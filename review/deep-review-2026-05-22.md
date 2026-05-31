# QsMessaging Deep Review

Дата проверки: 2026-05-22  
Репозиторий: `D:\Projects\QsMessaging`  
Scope: библиотека `QsMessaging`, unit tests, integration/example projects, package metadata, CI/CD, документация и текущее состояние working tree.

## Короткий итог

Проект собирается, unit tests проходят, NuGet vulnerability scan для основных runtime/test зависимостей не нашел известных уязвимых пакетов. Но для messaging-библиотеки есть несколько P0/P1 рисков: durable-семантика сообщений не гарантируется, ошибки handlers подтверждаются транспортам как успешно обработанные, request/response может зависнуть или течь по памяти при cancellation/timeout, а transport lifecycle держится на смеси transient DI и static state. Это не косметика: в production эти места могут давать потерю сообщений, зависшие запросы, дубли consumers/processors и трудно воспроизводимое поведение при перезапусках.

## Проверки

| Проверка | Результат |
| --- | --- |
| `dotnet build .\QsMessaging.sln -m:1` | OK, 3 warnings |
| `dotnet test .\QsMessagingUnitTests\QsMessagingUnitTests.csproj -m:1 --no-restore` | OK, 114 passed |
| `dotnet test ... --collect:"XPlat Code Coverage"` | OK, line coverage 48.09%, branch coverage 39.54% |
| Сборка проектов вне solution | OK: `CleanupArrangeInstance01`, `CleanupAssertInstance01`, все 4 `IntegrationTestAzureServiceBus` projects |
| `dotnet list .\QsMessaging\QsMessaging.csproj package --vulnerable --include-transitive` | No vulnerable packages |
| `dotnet list .\QsMessagingUnitTests\QsMessagingUnitTests.csproj package --vulnerable --include-transitive` | No vulnerable packages |
| `dotnet list ... package --outdated --include-transitive` | Есть устаревшие пакеты, включая `RabbitMQ.Client 7.0.0 -> 7.2.1`, test SDK/MSTest/coverlet |
| `dotnet build .\QsMessaging\QsMessaging.csproj -c Release -m:1` | OK, те же 3 warnings |
| `dotnet pack .\QsMessaging\QsMessaging.csproj --no-build -c Release` | OK после Release build |

Ограничение проверки: live integration scenarios с реальными RabbitMQ/Azure Service Bus не запускались, потому что это требует поднятых внешних сервисов и валидной инфраструктуры. Интеграционные проекты были собраны, но не выполнены как end-to-end сценарии.

## Состояние working tree

На момент review репозиторий не чистый:

| Файл | Состояние |
| --- | --- |
| `IntegrationTest/ArrangeInstance01/Properties/launchSettings.json` | modified: transport arg изменен с `AzureServiceBus` на `RabbitMq` |
| `IntegrationTest/RequestResponseInstance01/Properties/launchSettings.json` | modified: transport arg изменен с `AzureServiceBus` на `RabbitMq` |
| `IntegrationTest/RequestResponseInstance02/Properties/launchSettings.json` | modified: transport arg изменен с `AzureServiceBus` на `RabbitMq` |
| `QsMessaging/RabbitMq/Services/RqQueueService.cs.orig` | untracked |

Также в git уже лежат backup/orig файлы: `QsMessaging/AzureServiceBus/Services/AsbConsumerService.cs.orig`, `docs/index.html.orig`.

---

# P0 / Critical

## 1. Durable delivery заявлена, но сообщения теряются, если receiver queue/subscription еще не создана

Файлы:
- `QsMessaging/RabbitMq/RqSender.cs:134-149`
- `QsMessaging/RabbitMq/RqSubscriber.cs:36-41`
- `QsMessaging/AzureServiceBus/AsbSender.cs:25-29`
- `QsMessaging/AzureServiceBus/Services/AsbTopicSubscriptionService.cs:18-45`
- `QsMessaging/Public/IQsMessaging.cs:5-15`

`SendMessageAsync` документирован как durable/waiting flow, но sender создает только exchange/topic и не создает receiving queue/subscription. В RabbitMQ fanout exchange не хранит сообщения; если durable queue еще не была объявлена receiver-ом, publish уходит без маршрута. `mandatory: true` не спасает без BasicReturn/publisher-confirm handling. В Azure Service Bus topic без subscription также не сохраняет message для будущей subscription.

Impact: первый запуск sender до receiver-а или удаленная queue/subscription приводит к тихой потере обычных сообщений, хотя public API обещает обратное.

Рекомендация: либо изменить контракт API/документацию честно на "delivery only to existing bindings", либо ввести topology provisioning вне handler discovery: declarative topology config, startup provisioner, publisher confirms/returns, health checks и fail-fast при unroutable messages.

## 2. Ошибки handler-ов подтверждаются транспортам как успешная обработка

Файлы:
- `QsMessaging/Shared/Services/ConsumerService.cs:77-107`
- `QsMessaging/RabbitMq/Services/RqConsumerService.cs:58-64`
- `QsMessaging/AzureServiceBus/Services/AsbConsumerService.cs:39-42`
- `QsMessaging/AzureServiceBus/Services/AsbConsumerService.cs:91-100`

`UniversalConsumer` ловит ошибки handler/deserialization/request-response pipeline и не возвращает transport layer информацию о failure. RabbitMQ consumer всегда делает `BasicAckAsync` в `finally`; Azure Service Bus всегда вызывает `CompleteMessageAsync`.

Impact: durable message с business exception, serialization bug или transient DB error будет удален из broker-а. Error handler получает уведомление, но retry/dead-letter/requeue невозможны на уровне транспорта.

Рекомендация: разделить outcome обработки на success/failure. Для RabbitMQ делать `BasicAck` только после success, `BasicNack`/DLX/requeue policy для failure. Для Azure Service Bus использовать `Complete`, `Abandon`, `DeadLetter` по policy. Error handler не должен быть единственным recovery-механизмом.

## 3. Request/response может зависнуть при cancellation и течет по памяти при timeout

Файлы:
- `QsMessaging/Shared/RequestResponseMessageStore.cs:24-31`
- `QsMessaging/RabbitMq/RqSender.cs:81-114`
- `QsMessaging/AzureServiceBus/AsbSender.cs:43-63`

`AddRequestMessageAsync` создает `Task.Delay(config.RequestResponseTimeout, cancellationToken).ContinueWith(..., cancellationToken, ...)`. Если caller cancellation token отменяется, continuation тоже отменяется и `TaskCompletionSource` может остаться незавершенным навсегда. При timeout task fault-ится, но record не удаляется из static dictionary, потому что `RemoveMessage` вызывается только после successful await.

Impact: отмененный request/response может зависнуть навсегда; timeout оставляет request payload, TCS и metadata в static store до конца процесса.

Рекомендация: использовать `TaskCompletionSource<TResponse>` с отдельным timeout/cancellation registration, всегда удалять correlation record в `finally`, не передавать caller cancellation token в timeout continuation, явно завершать TCS cancellation-ом.

## 4. Azure `SendMessageAsync` публикует normal messages как event с TTL 60 секунд

Файл: `QsMessaging/AzureServiceBus/AsbSender.cs:25-29`, `QsMessaging/AzureServiceBus/AsbSender.cs:99-107`

`SendMessageAsync<TMessage>` вызывает `CreateMessage(..., MessageTypeEnum.Event)`. В результате обычное message получает `TimeToLive = 60 seconds`, а не `14 days`. Лог при этом говорит "queue", хотя фактически используется topic.

Impact: нормальные сообщения через Azure Service Bus истекают через минуту и не соответствуют semantics `IQsMessageHandler`.

Рекомендация: использовать `MessageTypeEnum.Message` для `SendMessageAsync`, явно тестировать TTL/content/routing для message vs event, привести naming/logging к topic/subscription модели или перейти на queue для command/message semantics.

---

# P1 / High

## 5. DI lifecycle смешан со static state, что ломает multi-host и restart сценарии

Файлы:
- `QsMessaging/Public/QsMessagingRegistering.cs:29-40`
- `QsMessaging/Public/QsMessagingRegistering.cs:86-125`
- `QsMessaging/Shared/Services/HandlerService.cs:14-15`
- `QsMessaging/Shared/RequestResponseMessageStore.cs:11`
- `QsMessaging/RabbitMq/Services/RqConsumerService.cs:23`
- `QsMessaging/RabbitMq/Services/RqExchangeService.cs:16-17`
- `QsMessaging/RabbitMq/Services/RqQueueService.cs:16-17`
- `QsMessaging/RabbitMq/Services/RbConnectionService.cs:10-11`
- `QsMessaging/AzureServiceBus/Services/AsbConnectionService.cs:13-15`

Stateful services в основном зарегистрированы как `Transient`, но состояние хранится в `static` collections/connections. Это дает глобальное состояние на весь процесс, не изолированное по `IHost`, конфигурации и тесту. `IQsMessagingConnectionManager` тоже transient: каждый `UseQsMessaging()`/`CleanUpTransportation()` может получить новый manager с новым `RqChannelService`, не тот, который открывал channels.

Impact: дубли handlers/consumers/processors, state leakage между тестами и hosts, collision временных queues/subscriptions, трудно закрываемые channels, непредсказуемое поведение при нескольких service providers в одном процессе.

Рекомендация: сделать lifecycle services singleton на service provider, убрать static caches, хранить state instance-scoped, добавить idempotent `Open/Close`, покрыть multi-host tests.

## 6. Handler discovery ограничен только entry assembly

Файл: `QsMessaging/Public/QsMessagingRegistering.cs:31`, `QsMessaging/Shared/Services/HandlerService.cs:25-33`

Handlers ищутся через `Assembly.GetEntryAssembly()!`. В реальных .NET приложениях handlers часто живут в отдельных class libraries. Сейчас public API не дает передать assemblies для scan-а, а null-forgiving может дать NullReference в нестандартных host/test contexts.

Impact: часть handlers не будет зарегистрирована, а sender при этом может успешно публиковать messages в topology без receiver-а.

Рекомендация: добавить `options.AssembliesToScan`, overload `AddQsMessaging(..., params Assembly[])`, fallback на calling assembly, fail-fast если найдено 0 handlers для expected contracts.

## 7. RabbitMQ `Close` не останавливает consumers явно

Файлы:
- `QsMessaging/Shared/Interface/ISubscriber.cs:7-9`
- `QsMessaging/RabbitMq/RqSubscriber.cs:10-43`
- `QsMessaging/RabbitMq/RqConnectionManager.cs:106-108`
- `QsMessaging/RabbitMq/Services/RqChannelService.cs:13-23`

`ISubscriber.CloseAsync` имеет default no-op, а `RqSubscriber` не переопределяет его. `RqConnectionManager.CloseCoreAsync` вызывает no-op close, затем закрывает channels только из своего `RqChannelService` instance. Из-за transient manager/channel service это может быть пустой cache.

Impact: consumers/channels зависят от side effect закрытия connection, static consumer store остается с consumer records, reopen/cleanup сценарии становятся хрупкими.

Рекомендация: реализовать `RqSubscriber.CloseAsync`, отменять `BasicCancelAsync` по stored consumer tags, сделать channel/consumer services singleton и очищать stores при close.

## 8. Full cleanup методы могут удалить весь RabbitMQ vhost или Azure namespace

Файлы:
- `QsMessaging/RabbitMq/RqTransportFullCleaner.cs:20-32`
- `QsMessaging/AzureServiceBus/AsbTransportFullCleaner.cs:26-54`
- `README.md:56-64`

`FullCleanUpTransportation()` удаляет все queues/topics/subscriptions/exchanges, видимые текущей конфигурации. RabbitMQ фильтрует только `amq.*`; Azure удаляет все topics/queues. Метод публичный и не имеет environment guard, prefix guard, dry-run, confirmation token или explicit dangerous option.

Impact: случайный вызов с production credentials может удалить чужую messaging infrastructure.

Рекомендация: требовать `AllowDangerousFullCleanup = true`, поддержать dry-run, ограничить default cleanup префиксом `Qs*`, логировать explicit target scope и запретить full cleanup при production environment без override.

## 9. Azure subscriber создает новые processors и подписчики при каждом open/subscribe

Файл: `QsMessaging/AzureServiceBus/AsbSubscriber.cs:28-40`

`SubscribeHandlerAsync` каждый раз получает processor, добавляет delegates и запускает processing. В `AsbServiceBusProcessorService.GetOrCreate` нет cache, despite name. `_processors` static bag только копит processors до close.

Impact: повторный `UseQsMessaging()` или runtime response handler subscription может создать duplicate processors/handlers, duplicate processing и resource leak.

Рекомендация: cache processors by entity/subscription/handler key, make subscribe idempotent, detach handlers on close, add tests for double open.

## 10. Solution/CI не покрывает все проекты репозитория

Файлы:
- `QsMessaging.sln`
- `azure-pipelines.yml:36-60`
- `azure-pipelines-develop.yml:42-66`

В solution не включены `IntegrationTest/CleanupArrangeInstance01`, `IntegrationTest/CleanupAssertInstance01` и все `IntegrationTestAzureServiceBus/*`. CI строит только solution, значит эти проекты могут сломаться незаметно. В рамках review они собраны отдельно и сейчас проходят.

Impact: release pipeline не защищает часть репозитория от compile regressions.

Рекомендация: добавить все maintained projects в solution или отдельный CI step `dotnet build` по всем `.csproj`; если какие-то проекты deprecated, явно вынести/архивировать.

## 11. CI publish steps могут публиковать пакет без branch/reason guard

Файлы:
- `azure-pipelines.yml:59-65`
- `azure-pipelines-develop.yml:65-70`

Pipeline всегда выполняет `dotnet nuget push` после build/test/pack. PR logic меняет version suffix, но publish step не имеет условия, запрещающего публикацию из PR/non-release runs. `azure-pipelines-develop.yml` при этом называется develop, но trigger настроен на `main`.

Impact: случайные pre-release/PR packages могут уйти в NuGet, а develop package может публиковаться из main.

Рекомендация: добавить `condition` на push: только protected branch, не PullRequest, только tags/release pipeline. Исправить trigger develop pipeline или переименовать файл.

## 12. Unit coverage низкий именно на критичных runtime paths

Файл coverage: `QsMessagingUnitTests/TestResults/.../coverage.cobertura.xml`

Итог coverage: line 48.09%, branch 39.54%. Полностью без line coverage в текущем run:

| File | Coverage |
| --- | --- |
| `Shared/Services/ConsumerService.cs` | 0% |
| `AzureServiceBus/AsbSender.cs` | 0% |
| `AzureServiceBus/Services/AsbConsumerService.cs` | 0% |
| `AzureServiceBus/AsbTransportFullCleaner.cs` | 0% |
| `RabbitMq/RqTransportCleaner.cs` | 0% |
| `RabbitMq/RqTransportFullCleaner.cs` | 0% |
| `RabbitMq/Services/RqManagementService.cs` | 0% |
| `QsMessagingGate.cs` | 0% |

Impact: tests зеленые, но главная handler pipeline, cleanup, ASB sender/consumer и public facade практически не проверены.

Рекомендация: сначала покрыть P0 paths: handler exception outcome, cancellation/timeout request-response, Azure message TTL, double open/close, unroutable publish.

---

# P2 / Medium

## 13. `RqExchangeService` продолжает работу после RabbitMQ 406 precondition failed

Файл: `QsMessaging/RabbitMq/Services/RqExchangeService.cs:33-47`

При mismatch exchange settings код логирует error, но возвращает exchange name и продолжает startup/publish. После 406 RabbitMQ обычно закрывает channel, так что последующие операции могут падать позже и менее понятно.

Рекомендация: fail-fast, пересоздать channel после 406 или явно вернуть typed topology mismatch exception.

## 14. `RbConnectionService.GetOrCreateConnectionAsync` может вернуть null и частично игнорирует cancellation

Файл: `QsMessaging/RabbitMq/Services/RbConnectionService.cs:55-116`

Метод объявлен как `Task<IConnection>`, но содержит suppressed `return null`. `WaitAsync()` на semaphore вызывается без cancellation token. Connection failures ловятся как `Exception`, retry идет бесконечно до cancellation.

Рекомендация: `await _semaphore.WaitAsync(cancellationToken)`, убрать nullable return path, ввести max retry/startup timeout или вернуть осмысленную failure policy.

## 15. Error classification не различает ошибки handler-а и ошибки receive/deserialization

Файл: `QsMessaging/Shared/Services/ConsumerService.cs:77-90`, `QsMessaging/Public/Handler/IQsMessagingConsumerErrorHandler.cs:43-47`

Inner catch вокруг handler execution помечает всё как `RecevingProblem`, хотя enum имеет `InHandlerProblem`. Сам enum содержит typo `RecevingProblem`.

Рекомендация: использовать `InHandlerProblem` для business handler exceptions; `ReceivingProblem` для deserialization/infrastructure. Исправление enum будет breaking change, поэтому лучше планировать major/minor migration.

## 16. Public facade cancellation handling не работает так, как выглядит

Файл: `QsMessaging/QsMessagingGate.cs:14-23`, `QsMessaging/QsMessagingGate.cs:53-64`

Методы возвращают task из sender без `await`, поэтому `try/catch OperationCanceledException` ловит только синхронный throw, не async cancellation/fault. В редком sync cancellation path `RequestResponse` вернет `Task.FromResult(null)` для non-nullable `TResponse`.

Рекомендация: сделать методы `async`, либо убрать catch и позволить cancellation распространяться стандартно.

## 17. RabbitMQ publish не использует publisher confirms/returns

Файл: `QsMessaging/RabbitMq/RqSender.cs:143-149`

Даже при `mandatory: true` код не обрабатывает unroutable returns и не включает publisher confirmations. Лог "Message has been published" означает только, что client отправил frame, а не что broker route/confirm состоялись.

Рекомендация: включить confirms для reliable message mode, обработать `BasicReturn`, вернуть exception caller-у при unroutable persistent messages.

## 18. RabbitMQ management client создается заново на каждый HTTP call и по умолчанию использует HTTP

Файлы:
- `QsMessaging/RabbitMq/Services/RqManagementService.cs:41-42`
- `QsMessaging/RabbitMq/Services/RqManagementService.cs:67-68`
- `QsMessaging/RabbitMq/Services/RqManagementService.cs:85-104`
- `QsMessaging/RabbitMq/Configuration.cs:55-59`

Каждый API call создает новый `HttpClient`, а default `ManagementScheme = "http"` отправляет Basic auth credentials без TLS, если пользователь не переопределил scheme/base address.

Рекомендация: использовать `IHttpClientFactory` или singleton `HttpClient`; default для non-localhost сделать `https`, документировать credentials transport security.

## 19. Package README и root README расходятся

Файлы:
- `QsMessaging/QsMessaging.csproj:16-23`, `QsMessaging/QsMessaging.csproj:37-42`
- `QsMessaging/README.md`
- `azure-pipelines.yml:46-60`

Package project включает `QsMessaging/README.md`, но этот файл сейчас содержит дублирующиеся секции RabbitMQ/Azure Service Bus. CI перед pack копирует root `README.md` в `QsMessaging/README.md`, то есть локальный `dotnet pack` и CI package могут иметь разный README.

Рекомендация: хранить единственный package README source или использовать MSBuild include на root README без pipeline mutation.

## 20. Build warnings не блокируют release

Файлы:
- `QsMessaging/AzureServiceBus/Services/AsbConsumerService.cs:19`
- `QsMessaging/AzureServiceBus/Services/AsbServiceBusProcessorService.cs:11`
- `QsMessaging/AzureServiceBus/Services/AsbServiceBusProcessorService.cs:47`
- `QsMessaging/QsMessaging.csproj`

Текущая сборка имеет warnings: unused `responseSender`, unused `logger`, unreachable code. В project file нет `TreatWarningsAsErrors`, analyzer rule set или documentation generation для NuGet API.

Рекомендация: включить warnings-as-errors хотя бы в CI для library project, убрать dead/unreachable code, добавить analyzers.

## 21. Azure/Rabbit serialization options не конфигурируются

Файлы:
- `QsMessaging/Shared/Services/ConsumerService.cs:31`
- `QsMessaging/RabbitMq/RqSender.cs:131`, `QsMessaging/RabbitMq/RqSender.cs:166`
- `QsMessaging/AzureServiceBus/AsbSender.cs:101`

Используется `JsonSerializer` с default options. Нет настройки naming policy, converters, polymorphism, enum strategy, required fields behavior. RabbitMQ также не ставит content-type в properties.

Рекомендация: добавить `JsonSerializerOptions` в configuration, content-type/version metadata, contract compatibility tests.

## 22. Topology creation делает admin exists/create на hot path

Файлы:
- `QsMessaging/AzureServiceBus/AsbSender.cs:27-35`
- `QsMessaging/AzureServiceBus/Services/AsbQueueService.cs:14-38`
- `QsMessaging/AzureServiceBus/Services/AsbTopicService.cs:13-31`

Каждый send/request может делать admin API `Exists`/`Create` через services без local cache/lock. На cloud namespace это лишняя latency и throttling risk.

Рекомендация: provision topology на startup, cache created entities per service provider, добавить locks на entity name.

## 23. `AsbConnectionService` и `RbConnectionService` имеют static connection при instance semaphore

Файлы:
- `QsMessaging/AzureServiceBus/Services/AsbConnectionService.cs:13-15`
- `QsMessaging/RabbitMq/Services/RbConnectionService.cs:10-11`

Сейчас services registered singleton, но сам код неустойчив к lifetime changes: connection static, semaphore instance-scoped. Это уже противоречит остальной transient/static архитектуре.

Рекомендация: либо полностью singleton instance state, либо static semaphore тоже static. Предпочтительнее убрать static.

## 24. `.orig`/excluded source files засоряют package repository

Файлы:
- `QsMessaging/AzureServiceBus/Services/AsbConsumerService.cs.orig`
- `docs/index.html.orig`
- `QsMessaging/RabbitMq/Services/RqQueueService.cs.orig`
- `QsMessaging/QsMessaging.csproj:31-35`

В репозитории есть backup `.orig` files, один из них untracked. В `.csproj` есть `Compile Remove` для нескольких source files, то есть dead code остается рядом с maintained code.

Рекомендация: удалить backup files, перенести deprecated code в историю git или отдельную docs note, убрать `Compile Remove` для dead code после cleanup.

---

# P3 / Low

## 25. `InstanceService.GetInstanceUID` логирует Information на каждый вызов

Файл: `QsMessaging/Shared/Services/InstanceService.cs:10-14`

Temporary queue/subscription name generation может часто дергать `GetInstanceUID`, что засоряет logs.

Рекомендация: log once at startup или `Debug/Trace`.

## 26. Naming typos уже попали в public/internal API

Файлы:
- `QsMessaging/Public/Handler/IQsMessagingConsumerErrorHandler.cs:43-47`
- `QsMessaging/Shared/Models/StoreMessageRecord.cs:3`
- `QsMessaging/RabbitMq/RqNameGenerator.cs:43`, `QsMessaging/RabbitMq/RqNameGenerator.cs:62`
- `QsMessaging/AzureServiceBus/Services/AsbNameGenerator.cs:32`

Примеры: `RecevingProblem`, `IsResponsed`, `banseQueueName`, `unknowType`. Public enum typo требует аккуратной migration.

## 27. `NameGeneratorBase.HashString` можно упростить

Файл: `QsMessaging/Shared/NameGeneratorBase.cs:20-28`

Можно заменить `SHA256.Create().ComputeHash` на `SHA256.HashData` и `Convert.ToHexString(...).ToLowerInvariant()`.

## 28. Test code содержит stale/disabled tests

Файлы:
- `QsMessagingUnitTests/AzureServiceBus/Services/AsbServiceBusProcessorServiceTest.cs:46-68`
- `QsMessagingUnitTests/AzureServiceBus/SubscriberTest.cs:54-61`, `QsMessagingUnitTests/AzureServiceBus/SubscriberTest.cs:80-86`

Часть тестов закомментирована, а `AsbServiceBusProcessorServiceTest.ClearProcessors` ищет `_processors` field в классе, где его уже нет. Сейчас это не падает только потому, что активных `[TestMethod]` в классе нет.

Рекомендация: удалить stale tests или восстановить актуальные tests на processor lifecycle.

## 29. GitHub Pages workflow привязан к ветке `AzureBus`

Файл: `.github/workflows/github-pages.yml:3-12`

Docs deploy не срабатывает на `main`, если текущая production branch действительно `main`. Возможно это намеренно, но выглядит как branch drift.

---

# Рекомендуемый порядок исправлений

1. Починить message outcome model: handler result должен управлять ack/complete/nack/dead-letter.
2. Починить request/response store: cancellation, timeout cleanup, `finally RemoveMessage`.
3. Сделать lifecycle idempotent и singleton-scoped, убрать static state из runtime services.
4. Определиться с durable topology contract: pre-provision queues/subscriptions или честно изменить API guarantees.
5. Исправить Azure `SendMessageAsync` TTL/message type и покрыть tests.
6. Расширить handler discovery assemblies.
7. Добавить CI guardrails: все projects, warnings-as-errors, no publish on PR, coverage threshold для core paths.
8. Ограничить full cleanup safety guards.
9. Почистить repository hygiene: `.orig`, excluded sources, package README, stale tests.

