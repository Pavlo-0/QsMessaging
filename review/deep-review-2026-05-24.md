# QsMessaging Deep Code Review

Дата проверки: 2026-05-24  
Репозиторий: `D:\Projects\QsMessaging`  
Scope: текущий проект: `QsMessaging`, unit tests, examples, integration projects, solution/CI. Сторонние библиотеки, их CVE/outdated/license-состояние и внешний код не проверялись.

## Короткий итог

Код заметно продвинулся относительно предыдущего review: добавлен explicit assembly scanning, исправлен request/response timeout/cancellation leak, `SendMessageAsync` для Azure Service Bus теперь использует message TTL, full cleanup по умолчанию ограничен QsMessaging-префиксами. Сборка и unit tests зеленые.

Главный риск все еще в runtime-семантике messaging: handler pipeline не сообщает transport layer результат обработки, поэтому RabbitMQ делает `BasicAck`, а Azure Service Bus делает `CompleteMessageAsync` даже после deserialization/handler/response-send failure. Для библиотеки сообщений это означает потерю durable-сообщений на unhappy path. Второй крупный пласт: много process-wide `static` state в сервисах, которые зарегистрированы как DI-сервисы; это ломает multi-host/multi-config сценарии, усложняет cleanup/reopen и уже вынуждает unit tests чистить internals через reflection.

## Проверки

| Проверка | Результат |
| --- | --- |
| `dotnet build .\QsMessaging.sln -m:1 --no-restore` | OK, 3 warnings |
| `dotnet test .\QsMessagingUnitTests\QsMessagingUnitTests.csproj -m:1 --no-restore` | OK, 150 passed |
| `dotnet sln .\QsMessaging.sln list` vs `rg --files -g '*.csproj'` | В solution не входят 6 проектов |
| Отдельная сборка проектов вне solution | OK: `IntegrationTest/CleanupArrangeInstance01`, `IntegrationTest/CleanupAssertInstance01`, все 4 `IntegrationTestAzureServiceBus/*` |

Warnings в основной библиотеке:

| Warning | Файл |
| --- | --- |
| `CS9113` unread `responseSender` | `QsMessaging/AzureServiceBus/Services/AsbConsumerService.cs:19` |
| `CS9113` unread `logger` | `QsMessaging/AzureServiceBus/Services/AsbServiceBusProcessorService.cs:11` |
| `CS0162` unreachable code | `QsMessaging/AzureServiceBus/Services/AsbServiceBusProcessorService.cs:47` |

End-to-end интеграционные сценарии с реальными RabbitMQ/Azure Service Bus не запускались: они требуют поднятой внешней инфраструктуры и валидных настроек. Проверка сторонних библиотек намеренно не выполнялась.

## Состояние working tree

На момент review рабочее дерево уже было dirty, эти изменения не трогались:

| Файл | Состояние |
| --- | --- |
| `Examples/RequestResponse.RequestInstance/Properties/launchSettings.json` | modified |
| `Examples/RequestResponse.ResponseInstance/Properties/launchSettings.json` | modified |
| `IntegrationTest/ArrangeInstance01/Properties/launchSettings.json` | modified |
| `IntegrationTest/RequestResponseInstance01/Properties/launchSettings.json` | modified |
| `IntegrationTest/RequestResponseInstance02/Properties/launchSettings.json` | modified |
| `review/` | untracked, уже содержал предыдущее review |

---

# P0 / Critical

## 1. Ошибки обработки подтверждаются брокеру как success

Файлы:
- `QsMessaging/Shared/Services/ConsumerService.cs:64-78`
- `QsMessaging/Shared/Services/ConsumerService.cs:96-106`
- `QsMessaging/RabbitMq/Services/RqConsumerService.cs:68-81`
- `QsMessaging/AzureServiceBus/Services/AsbConsumerService.cs:39-45`
- `QsMessaging/AzureServiceBus/Services/AsbConsumerService.cs:95-104`

`ConsumerService.UniversalConsumer` ловит исключения handler-а, deserialization и response pipeline, вызывает error handlers и возвращает `Task` без failure-state. Ни RabbitMQ, ни Azure layer не могут отличить успешную обработку от failed-after-retry.

В RabbitMQ `RqConsumerService` после любого исхода заходит в `finally` и делает `BasicAckAsync`. В Azure Service Bus `AsbConsumerService` после любого исхода делает `CompleteMessageAsync`. `TryNegativeAckAsync` в RabbitMQ есть, но вызов закомментирован, а `ConsumerService` все равно не пробрасывает failure.

Impact: durable-сообщение, которое упало на business exception, JSON error, transient DB error или ошибке отправки response, удаляется из транспорта. Error handler получает уведомление, но транспортного retry, requeue, abandon или dead-letter больше не будет.

Рекомендация: вернуть из shared consumer явный outcome, например `Consumed`, `Failed`, `Cancelled`, `Poison`. RabbitMQ: `BasicAck` только на success, `BasicNack`/DLX/requeue по policy на failure. Azure Service Bus: `Complete` только на success, `Abandon`/`DeadLetter` по policy на failure. Error handler должен быть observability/recovery extension, а не единственным механизмом надежности.

## 2. Request/response может потерять ответ после успешного handler-а

Файлы:
- `QsMessaging/Shared/Services/ConsumerService.cs:82-88`
- `QsMessaging/Shared/Services/ConsumerService.cs:96-106`
- `QsMessaging/AzureServiceBus/AsbSender.cs:100-117`
- `QsMessaging/RabbitMq/Services/RqConsumerService.cs:73-81`
- `QsMessaging/AzureServiceBus/Services/AsbConsumerService.cs:43-45`

Для `IQsRequestResponseHandler<,>` handler может успешно посчитать response, но `SendMessageCorrelatedAsync` затем может не доставить его. В Azure путь особенно опасный: если `ReplyTo` пустой или response queue уже удалена, `AsbSender.SendMessageCorrelatedAsync` логирует warning/information и возвращает success. После этого request message все равно подтверждается транспорту.

Impact: caller получает timeout, хотя request был обработан; исходное request-сообщение уже ack/complete и не будет обработано повторно. Это создает "ghost success" на стороне consumer-а и failure на стороне caller-а.

Рекомендация: response send должен быть частью atomic outcome обработки request. Если response не отправлен, request consumer должен завершиться failure outcome и дать transport policy решить requeue/abandon/dead-letter. Для Azure missing reply queue лучше завершать как failure или явно dead-letter request с диагностикой.

## 3. Process-wide static state смешан с DI lifecycle

Файлы:
- `QsMessaging/Shared/Services/HandlerService.cs:14-17`
- `QsMessaging/RabbitMq/Services/RbConnectionService.cs:10`
- `QsMessaging/AzureServiceBus/Services/AsbConnectionService.cs:13-14`
- `QsMessaging/AzureServiceBus/AsbSubscriber.cs:18`
- `QsMessaging/RabbitMq/Services/RqConsumerService.cs:23`
- `QsMessaging/RabbitMq/Services/RqExchangeService.cs:16-17`
- `QsMessaging/RabbitMq/Services/RqQueueService.cs:16-17`
- `QsMessaging/Shared/RequestResponseMessageStore.cs:11`
- `QsMessaging/Shared/Services/InstanceService.cs:8-9`
- `QsMessaging/Public/QsMessagingRegistering.cs:64-72`
- `QsMessaging/Public/QsMessagingRegistering.cs:166-180`
- `QsMessaging/Public/QsMessagingRegistering.cs:184-197`

Многие сервисы зарегистрированы как `Transient`/`Singleton`, но их состояние живет в `static` коллекциях/connection fields. Это не изолировано по `IServiceProvider`, `IHost`, конфигурации, transport или тесту.

Examples:
- второй host с другой RabbitMQ/ASB конфигурацией может переиспользовать connection/client первого host-а;
- `HandlerService.RegisterAllHandlers()` регистрирует все handlers из static bag, включая найденные предыдущими service providers;
- `AsbSubscriber.CloseAsync()` очищает static `_processors`, потенциально закрывая processors другого host-а;
- tests вынуждены сбрасывать private static bags через reflection, что само по себе сигнализирует о скрытой глобальной зависимости.

Impact: непредсказуемое поведение в test runner, worker process с несколькими hosts, integration scenarios, restart/cleanup/reopen flows. Ошибки будут выглядеть как дубли consumers/processors, подписки на чужие handlers, reuse не той connection string и cleanup чужих in-process subscriptions.

Рекомендация: убрать mutable static state из runtime-сервисов. State должен жить в singleton instance внутри конкретного service provider. Если нужен process-wide registry, его надо явно моделировать с ключом transport/config/service-provider и lifecycle ownership. Для tests убрать reflection resets.

---

# P1 / High

## 4. `UseQsMessaging()` может зависнуть навсегда при недоступном RabbitMQ

Файлы:
- `QsMessaging/Public/QsMessagingRegistering.cs:117-123`
- `QsMessaging/RabbitMq/RqConnectionManager.cs:38-48`
- `QsMessaging/RabbitMq/Services/RbConnectionService.cs:73-105`

`UseQsMessaging()` не принимает `CancellationToken`. `RqConnectionManager.Open()` вызывает `OpenCoreAsync(CancellationToken.None)`. Внутри `RbConnectionService.GetOrCreateConnectionAsync` идет цикл `while (!cancellationToken.IsCancellationRequested)` с backoff до 30 секунд, но при `CancellationToken.None` он бесконечный.

Impact: если RabbitMQ недоступен или credentials неверные, startup может зависнуть без явного fail-fast и без возможности отмены через host shutdown token.

Рекомендация: добавить overload `UseQsMessaging(this IHost host, CancellationToken cancellationToken)`, прокинуть token в `Open`, ввести max startup retry/timeout или использовать `Resilience`/отдельную connection policy. Для worker startup лучше fail-fast после ограниченного числа попыток.

## 5. Public send API может завершиться success при фактически недоставленном message

Файлы:
- `QsMessaging/Public/IQsMessaging.cs:5-15`
- `QsMessaging/Public/Handler/IQsMessageHandler.cs:3-13`
- `QsMessaging/RabbitMq/RqSender.cs:245-255`
- `QsMessaging/AzureServiceBus/AsbSender.cs:180-187`
- `README.md:101-127`

README уже честнее описывает fast send path и limitation, но public XML comments все еще обещают, что message "will wait" до получения. Код при missing/unroutable receiver после retry логирует warning и возвращает `false`/swallows внутри sender path; `IQsMessaging.SendMessageAsync` наружу не возвращает delivery status.

Для Azure Service Bus отдельная проблема: send в существующий topic без subscription обычно будет accepted самим сервисом, и библиотека это не заметит без management pre-check. Для RabbitMQ unroutable message может быть пойман через mandatory/publisher-return, но после retry failure также не пробрасывается наружу.

Impact: caller видит успешный `await SendMessageAsync`, хотя message не был опубликован или не имеет receiver-а. Для durable semantics это опаснее, чем явный exception.

Рекомендация: либо изменить public contract на best-effort и вернуть delivery result, либо fail-fast exception после exhausted retries для durable `SendMessageAsync`. Документацию и XML comments надо привести к одному поведению.

## 6. Azure processors не кэшируются, несмотря на имя `GetOrCreate`

Файлы:
- `QsMessaging/AzureServiceBus/Services/AsbServiceBusProcessorService.cs:18-50`
- `QsMessaging/AzureServiceBus/AsbSubscriber.cs:28-43`
- `QsMessaging/AzureServiceBus/AsbSubscriber.cs:52-55`

`AsbServiceBusProcessorService.GetOrCreate` каждый раз создает новый `ServiceBusProcessor`. `AsbSubscriber.SubscribeHandlerAsync` каждый раз навешивает delegates и стартует processing. Ключа idempotency по queue/topic/subscription/handler нет.

Impact: повторный `UseQsMessaging()`, повторная подписка response handler-а или несколько transient subscribers могут создать duplicate processors и duplicate handlers. Static `_processors` затем закрывает их глобально, а не в рамках владельца.

Рекомендация: сделать processor registry instance-scoped singleton с ключом `(entity, subscription?, handler type, generic type)`, повторный subscribe должен быть no-op. На close надо detach handlers и dispose только processors, которыми владеет текущий subscriber.

## 7. Azure entity caches переживают cleanup и могут ломать reopen в том же процессе

Файлы:
- `QsMessaging/AzureServiceBus/Services/AsbTopicService.cs:14-22`
- `QsMessaging/AzureServiceBus/Services/AsbQueueService.cs:15-23`
- `QsMessaging/AzureServiceBus/AsbTransportCleaner.cs:19-80`
- `QsMessaging/AzureServiceBus/AsbTransportFullCleaner.cs:15-90`

`AsbTopicService` и `AsbQueueService` держат static `existingTopics`/`existingQueues`. Cleanup/full cleanup удаляет entities через admin client, но эти caches не инвалидируются. После cleanup в том же процессе `GetOrCreateTopicAsync` может вернуть cached topic name без реальной проверки/создания. Sender частично лечит missing entity через `InvalidateTopic/InvalidateQueue`, но subscriber path сначала создает processor/subscription и может упасть на несуществующем topic.

Impact: сценарий `CleanUpTransportation(); FullCleanUpTransportation(); UseQsMessaging();` может быть flaky: библиотека считает topology существующей, broker уже нет.

Рекомендация: cleanup должен инвалидировать все affected caches, а лучше убрать static caches и проверять entity existence в idempotent topology provisioner-е. Минимум: expose clear/invalidate-all на queue/topic services и вызывать после delete.

## 8. RabbitMQ exchange declare с 406 продолжает работу на потенциально закрытом channel

Файл:
- `QsMessaging/RabbitMq/Services/RqExchangeService.cs:47-58`

Если exchange существует с другой конфигурацией, RabbitMQ обычно отвечает 406 `PRECONDITION_FAILED` и закрывает channel. Код логирует error, но не бросает exception и возвращает exchange name. Следующие операции bind/publish пойдут через channel, который уже мог быть закрыт, и упадут позже с менее понятной причиной.

Impact: startup/publish может продолжить как будто topology валидна, а реальная ошибка проявится позднее и дальше от root cause.

Рекомендация: на 406 бросать domain exception с именем exchange и ожидаемой конфигурацией. После 406 invalidировать/закрывать channel и останавливать startup.

## 9. Request/response routing не защищен от нескольких handlers с одинаковым request type

Файлы:
- `QsMessaging/Shared/Services/HandlerService.cs:155-159`
- `QsMessaging/RabbitMq/RqSender.cs:110-116`
- `QsMessaging/AzureServiceBus/AsbSender.cs:72-77`
- `QsMessaging/RabbitMq/RqNameGenerator.cs:45-52`
- `QsMessaging/AzureServiceBus/Services/AsbNameGenerator.cs:16-20`

Для `IQsRequestResponseHandler<TRequest, TResponse>` handler discovery сохраняет в `GenericType` только первый generic argument, то есть `TRequest`. Transport queue/exchange для request тоже строится только по `TRequest`. Если в процессе появятся два handlers с одним `TRequest`, но разным `TResponse`, они конкурируют за одну очередь, и caller может ждать один response type, а request обработает другой handler.

Impact: nondeterministic request/response, runtime cast/deserialization surprises, timeouts.

Рекомендация: валидировать uniqueness `(TRequest)` для request handlers или включить response type/handler identity в routing key/entity name. На registration лучше fail-fast с понятным exception.

---

# P2 / Medium

## 10. Handler discovery игнорирует несколько контрактов на одном handler class

Файл:
- `QsMessaging/Shared/Services/HandlerService.cs:145-159`

`FindHandlers` ищет types, у которых есть нужный generic interface, но затем берет `First(...)` interface и добавляет один record. Если один class реализует `IQsMessageHandler<A>` и `IQsMessageHandler<B>`, второй contract будет проигнорирован.

Impact: часть handlers не подпишется на свои queues/topics без ошибки при registration.

Рекомендация: для каждого `handlerType` добавлять record на каждый matching interface, не только на первый. Добавить unit test для multi-contract handler.

## 11. `assembly.GetTypes()` без обработки `ReflectionTypeLoadException`

Файлы:
- `QsMessaging/Shared/Services/HandlerService.cs:109`
- `QsMessaging/Shared/Services/HandlerService.cs:145`

Handler discovery напрямую вызывает `assembly.GetTypes()`. В реальных приложениях assembly может содержать типы с optional dependencies, которые не загрузились в текущем process. Тогда registration упадет целиком, хотя QsMessaging handlers могли быть доступны.

Impact: brittle startup при plugin/module-heavy приложениях.

Рекомендация: обрабатывать `ReflectionTypeLoadException`, логировать loader exceptions, продолжать по `Types.Where(t != null)` или fail-fast с полным диагностическим сообщением.

## 12. Public send methods не принимают cancellation token и глотают `OperationCanceledException`

Файлы:
- `QsMessaging/Public/IQsMessaging.cs:15`
- `QsMessaging/Public/IQsMessaging.cs:26`
- `QsMessaging/QsMessagingGate.cs:18-21`
- `QsMessaging/QsMessagingGate.cs:33-35`

`SendMessageAsync` и `SendEventAsync` не дают caller-у cancellation token. Если внутри sender возникает `OperationCanceledException`, gate логирует и не пробрасывает исключение. Для `RequestResponse` cancellation пробрасывается наружу, то есть API ведет себя по-разному.

Impact: caller не может отличить successful send от отмененного send.

Рекомендация: добавить overloads с `CancellationToken`, не глотать cancellation без явного contract-а. Если backward compatibility важна, новые overloads могут стать основным путем, старые вызывать их с `CancellationToken.None`.

## 13. Solution/CI не покрывает все maintained projects

Файлы:
- `QsMessaging.sln`
- `azure-pipelines.yml:36-64`
- `azure-pipelines-develop.yml:42-70`

В репозитории 18 `.csproj`, а solution содержит 12. Вне solution сейчас остаются:

| Project |
| --- |
| `IntegrationTest/CleanupArrangeInstance01/CleanupArrangeInstance01.csproj` |
| `IntegrationTest/CleanupAssertInstance01/CleanupAssertInstance01.csproj` |
| `IntegrationTestAzureServiceBus/ArrangeInstance01/ArrangeInstance01.AzureServiceBus.csproj` |
| `IntegrationTestAzureServiceBus/AssertInstance01/AssertInstance01.AzureServiceBus.csproj` |
| `IntegrationTestAzureServiceBus/RequestResponseInstance01/RequestResponseInstance01.AzureServiceBus.csproj` |
| `IntegrationTestAzureServiceBus/RequestResponseInstance02/RequestResponseInstance02.AzureServiceBus.csproj` |

Отдельная сборка этих проектов сейчас проходит, но CI строит solution и не защищает их от будущих compile regressions. Publish step в обоих pipelines также не имеет branch/reason guard; PR logic меняет version suffix, но `dotnet nuget push` все равно стоит без условия.

Рекомендация: добавить все maintained projects в solution или отдельный CI step, который строит все `.csproj`. Для publish добавить condition: не PR, protected branch/tag/release pipeline.

## 14. Unit tests фиксируют текущий рискованный ack behavior

Файлы:
- `QsMessagingUnitTests/RabbitMq/Services/ConsumerServiceTest.cs:217-238`
- `QsMessagingUnitTests/AzureServiceBus/Services/AsbConsumerServiceTest.cs:25-63`
- `QsMessagingUnitTests/Shared/Services/ConsumerServiceTest.cs:115-140`
- `QsMessagingUnitTests/Shared/Services/ConsumerServiceTest.cs:144-172`

RabbitMQ tests проверяют, что при exception из inner consumer все равно вызывается `BasicAckAsync`, а `BasicNackAsync` не вызывается. Azure consumer test проверяет delegation в `UniversalConsumer`, но не проверяет settlement behavior при success/failure.

Impact: текущий test suite защищает поведение, которое для production messaging выглядит как loss-on-failure.

Рекомендация: после изменения consumer outcome переписать tests: success => ack/complete, handler failure => nack/abandon/dead-letter по policy, deserialization failure => poison/dead-letter или error policy, cancellation during shutdown => no ack/settlement unless handler actually finished.

## 15. Compile warnings указывают на мертвый/разошедшийся код

Файлы:
- `QsMessaging/AzureServiceBus/Services/AsbConsumerService.cs:19`
- `QsMessaging/AzureServiceBus/Services/AsbServiceBusProcessorService.cs:11`
- `QsMessaging/AzureServiceBus/Services/AsbServiceBusProcessorService.cs:45-47`

`responseSender` в `AsbConsumerService` не используется, `logger` в `AsbServiceBusProcessorService` не используется, после `throw` стоит unreachable `break`. Это небольшие вещи, но для messaging code они мешают видеть настоящие warnings.

Рекомендация: удалить unused dependencies/unreachable code или использовать logger для diagnostics. В CI лучше включить warning budget или `TreatWarningsAsErrors` хотя бы для release/package path.

---

# Что уже закрыто относительно прошлого review

| Было | Сейчас |
| --- | --- |
| Handler discovery только entry assembly | Добавлены calling assembly, `AssembliesToScan` и overload с assemblies |
| Request/response timeout/cancellation мог оставлять record | Store удаляет record на timeout/cancellation, sender удаляет в `finally` |
| Azure `SendMessageAsync` использовал event TTL | Теперь `MessageTypeEnum.Message`, TTL 14 days |
| Full cleanup удалял слишком широко | По умолчанию ограничен `Qs:`/`Qs-`/`Qs_`, dangerous cleanup за флагом |
| RabbitMQ subscriber close был no-op | `RqSubscriber.CloseAsync` теперь вызывает `consumerService.CloseAsync` |

# Приоритетный план исправлений

1. Ввести `ConsumerOutcome` и перестроить ack/complete/nack/abandon/dead-letter вокруг него. Это главный reliability fix.
2. Убрать mutable static state из connection/subscriber/handler/entity stores или четко привязать его к service provider/config scope.
3. Сделать lifecycle API отменяемым: `UseQsMessaging(..., CancellationToken)`, bounded startup retry/timeout.
4. Сделать Azure/RabbitMQ subscribe/open idempotent, особенно для `AsbServiceBusProcessorService`.
5. Привести public API/docs/tests к одной truth: best-effort send или durable/fail-fast send.
6. Добавить tests на multi-host, duplicate `UseQsMessaging`, handler failure settlement, response-send failure и duplicate request handler validation.
