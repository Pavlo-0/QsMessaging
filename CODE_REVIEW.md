# QsMessaging — Deep Code Review

Reviewed: 2026-03-29  
Scope: Full library (`QsMessaging/`), unit tests (`QsMessagingUnitTests/`), public API, DI registration, RabbitMQ integration layer.

---

## CRITICAL

### 1. Credentials exposed in plain text — no secure configuration support

**File:** [QsMessaging/RabbitMq/Configuration.cs](QsMessaging/RabbitMq/Configuration.cs#L40-L45)  
`QsRabbitMQConfiguration` stores `UserName` and `Password` as plain `string` properties with defaults `"guest"/"guest"`. There is no support for reading credentials from environment variables, secrets managers, or `IConfiguration`. Consumers of this library are forced to pass credentials through the `Action<IQsMessagingConfiguration>` delegate, which encourages hard-coding secrets.  
**Recommendation:** Accept `IConfiguration` or support environment-variable-based credential injection. At minimum, remove the default `guest/guest` values so users are forced to provide credentials explicitly.

### 2. `RequestResponseMessageStore` registered as Transient — state is lost between resolves

**File:** [QsMessaging/Public/QsMessagingRegistering.cs](QsMessaging/Public/QsMessagingRegistering.cs#L47)  
`IRequestResponseMessageStore` is registered as **Transient**, but the class uses a `static ConcurrentDictionary` to hold pending requests. This works _only by accident_ because of the static field. If the static field were ever refactored to an instance field (the natural pattern), all in-flight request-response correlations would silently break. Services that hold cross-request state (`RequestResponseMessageStore`, `ConsumerService`, `ExchangeService`, `QueueService`, `ChannelService`) should be registered as **Singleton**.  
**Recommendation:** Register `IRequestResponseMessageStore`, `IConsumerService`, `IExchangeService`, `IQueueService`, and `IChannelService` as Singleton — or remove the `static` modifier from their backing collections and register as Singleton.

### 3. `autoAck: true` — messages are lost on consumer failure

**File:** [QsMessaging/RabbitMq/Services/ConsumerService.cs](QsMessaging/RabbitMq/Services/ConsumerService.cs#L105)  
`BasicConsumeAsync` is called with `autoAck: true`. If the consumer throws an exception during processing, the message has already been acknowledged and is permanently lost. For persistent messages (`IQsMessageHandler`), this defeats the purpose of durability.  
**Recommendation:** Use `autoAck: false` for persistent message handlers and explicitly call `BasicAckAsync` after successful processing. Keep `autoAck: true` only for events (`IQsEventHandler`) where loss is acceptable.

### 4. Consumer handler task result not awaited — fire-and-forget in `MessageEventConsumer`

**File:** [QsMessaging/RabbitMq/Services/ConsumerService.cs](QsMessaging/RabbitMq/Services/ConsumerService.cs#L59-L61)  
In the `MessageEventConsumer` case, the invoke result is assigned to `resultAsync` but **never awaited**:

```csharp
case ConsumerPurpose.MessageEventConsumer:
    var resultAsync = consumeMethod.Invoke(handlerInstance, new[] { modelInstance });
    break;
```

If the handler is async (returns `Task`), exceptions are silently swallowed and the consumer pipeline has no backpressure. Processing may overlap in unexpected ways.  
**Recommendation:** Cast the result to `Task` and await it: `if (resultAsync is Task t) await t;`

---

## HIGH

### 5. `ConnectionService` semaphore is instance-scoped but connection is static — deadlock risk

**File:** [QsMessaging/RabbitMq/Services/ConnectionService.cs](QsMessaging/RabbitMq/Services/ConnectionService.cs#L11-L12)  
`connection` is `static` (shared across all instances) but `_semaphore` is an instance field. Because `ConnectionService` is registered as Singleton this currently works, but the asymmetry is a latent bug. If it were ever registered as Transient (like most other services), multiple semaphore instances would fail to protect the static connection, causing race conditions.  
**Recommendation:** Make the semaphore `static` to match the connection, or remove `static` from both and rely on the Singleton lifetime.

### 6. `GetOrCreateConnectionAsync` returns `null` after cancellation — contract violation

**File:** [QsMessaging/RabbitMq/Services/ConnectionService.cs](QsMessaging/RabbitMq/Services/ConnectionService.cs#L68-L72)  
After the loop, if cancellation was requested, `ThrowIfCancellationRequested()` is called, but the code path also has a `return null` that is suppressed with `#pragma warning disable CS8603`. This is confusing. The method signature returns `Task<IConnection>` (non-nullable) but can return `null`.  
**Recommendation:** Always throw on cancellation or on connection failure. Remove the `return null` path entirely.

### 7. Static mutable state across all services — no isolation, no testability

**Files:** [ConsumerService.cs](QsMessaging/RabbitMq/Services/ConsumerService.cs#L17), [ExchangeService.cs](QsMessaging/RabbitMq/Services/ExchangeService.cs#L13), [QueueService.cs](QsMessaging/RabbitMq/Services/QueueService.cs#L14), [ChannelService.cs](QsMessaging/RabbitMq/Services/ChannelService.cs#L12), [HandlerService.cs](QsMessaging/RabbitMq/Services/HandlerService.cs#L14-L15), [RequestResponseMessageStore.cs](QsMessaging/RabbitMq/RequestResponseMessageStore.cs#L11)  
Almost every service uses `static` collections (`ConcurrentBag`, `ConcurrentDictionary`). This means:

- State leaks between unit tests (test pollution).
- Multiple `IHost` instances in the same process share state unexpectedly.
- Cannot run integration tests in parallel.

**Recommendation:** Remove `static` from all backing collections and register the services as Singleton (or Scoped where appropriate).

### 8. `QsMessagingGate` swallows `OperationCanceledException` and returns null/completed task

**File:** [QsMessaging/QsMessagingGate.cs](QsMessaging/QsMessagingGate.cs#L18-L25)  
All three public methods catch `OperationCanceledException`, log it as `Information`, and return a completed/null task. The caller has no way to know the operation was cancelled — cancellation is silently swallowed. The `RequestResponse` method returns `null` for a non-nullable `TResponse`.  
**Recommendation:** Let `OperationCanceledException` propagate to callers. This is the standard .NET pattern — callers expect it and handle it.

### 9. Duplicate `IExchangeService` registration

**File:** [QsMessaging/Public/QsMessagingRegistering.cs](QsMessaging/Public/QsMessagingRegistering.cs#L34-L36)  
`IExchangeService` is registered twice as Transient:

```csharp
services.AddTransient<IExchangeService, ExchangeService>();  // line ~34
services.AddTransient<IExchangeService, ExchangeService>();  // line ~36
```

While harmless (last-wins in MS DI), it indicates copy-paste error and may confuse readers.  
**Recommendation:** Remove the duplicate registration.

### 10. `ErrorConsumerType` mislabeled in inner catch — should be `InHandlerProblem`

**File:** [QsMessaging/RabbitMq/Services/ConsumerService.cs](QsMessaging/RabbitMq/Services/ConsumerService.cs#L88-L98)  
The inner `catch` block (which catches exceptions thrown _by the handler_) labels the error as `ErrorConsumerType.RecevingProblem` instead of `ErrorConsumerType.InHandlerProblem`. This wrong classification means error handlers cannot correctly distinguish between deserialization/infrastructure errors and business-logic handler errors.  
**Recommendation:** Use `ErrorConsumerType.InHandlerProblem` in the inner catch block.

---

## MEDIUM

### 11. No message acknowledgment strategy — no retry, no dead-letter queue

There is no dead-letter exchange (DLX), no retry logic, and no Nack support. Failed messages are simply lost (see #3). For a production messaging library, this is a significant gap.  
**Recommendation:** Add DLX configuration and support `BasicNackAsync` with requeue policies for persistent messages.

### 12. `HardConfiguration.SupportedInterfaces` allocates a new array on every property access

**File:** [QsMessaging/RabbitMq/HardConfiguration.cs](QsMessaging/RabbitMq/HardConfiguration.cs#L32-L80)  
The `SupportedInterfaces` property creates a new `SupportedInterfacesStruct[]` on every call. It is called by `SupportedInterfacesTypes`, `GetExchangePurpose`, `GetQueuePurpose`, `GetChannelPurpose`, `GetConsumerPurpose` — all hot path or startup path methods.  
**Recommendation:** Cache the array in a `static readonly` field.

### 13. `ExchangeService` silently continues after exchange declaration failure (406)

**File:** [QsMessaging/RabbitMq/Services/ExchangeService.cs](QsMessaging/RabbitMq/Services/ExchangeService.cs#L31-L36)  
When exchange declaration fails with a 406 (precondition-failed / settings mismatch), the error is logged but execution continues. The exchange name is still returned and used, which will lead to publish failures downstream that are harder to diagnose.  
**Recommendation:** Throw or return a clear error so the startup fails fast.

### 14. `QueueService` — `x-expires: 0` is invalid for RabbitMQ

**File:** [QsMessaging/RabbitMq/Services/QueueService.cs](QsMessaging/RabbitMq/Services/QueueService.cs#L40-L42)  
Setting `x-expires` to `0` is not a valid value. RabbitMQ requires `x-expires` to be a positive integer (milliseconds). A value of `0` will cause a channel error. If the intent is "expire immediately when unused", use a small positive value or rely on `autoDelete` alone.  
**Recommendation:** Remove `x-expires: 0` or set a meaningful positive value.

### 15. `Sender` variable named `queuesService` is actually `IExchangeService`

**File:** [QsMessaging/RabbitMq/Sender.cs](QsMessaging/RabbitMq/Sender.cs#L14)  
The constructor parameter `IExchangeService queuesService` is misleadingly named. It's used for exchange operations, not queue operations.  
**Recommendation:** Rename to `exchangeService`.

### 16. `HandlerService.AddRRResponseHandler<TContract>` adds duplicates on every request

**File:** [QsMessaging/RabbitMq/Services/HandlerService.cs](QsMessaging/RabbitMq/Services/HandlerService.cs#L34-L44)  
Every `RequestResponse` call invokes `AddRRResponseHandler<TResponse>()`, which adds a new record to the static `_handlers` bag unconditionally. Over time, this causes unbounded growth and duplicate subscriptions.  
**Recommendation:** Check if a handler for this contract type already exists before adding.

### 17. No connection/channel disposal on application shutdown

There is no `IHostedService` or `IAsyncDisposable` implementation that closes connections and channels on graceful shutdown. The `IQsMessagingConnectionManager.Close()` exists but is not called automatically.  
**Recommendation:** Implement `IHostApplicationLifetime.ApplicationStopping` callback or an `IHostedService` to close connections during shutdown.

### 18. `InstanceService.GetInstanceUID` logs at `Information` level on every call

**File:** [QsMessaging/RabbitMq/Services/InstanceService.cs](QsMessaging/RabbitMq/Services/InstanceService.cs#L14)  
The instance UID is logged every time `GetInstanceUID()` is called, which can be frequent (once per queue name generation). This pollutes logs.  
**Recommendation:** Log at `Trace` or `Debug` level, or log only once.

### 19. String interpolation in logger calls — structured logging not used consistently

**Files:** [ConnectionService.cs](QsMessaging/RabbitMq/Services/ConnectionService.cs#L44), [QsMessagingGate.cs](QsMessaging/QsMessagingGate.cs#L22), [InstanceService.cs](QsMessaging/RabbitMq/Services/InstanceService.cs#L14)  
Several places use `$"..."` interpolation inside `logger.Log*()` calls instead of structured logging placeholders. This defeats structured logging (e.g., Serilog, Application Insights) and causes unnecessary string allocations even when the log level is disabled.  
**Recommendation:** Use `logger.LogInformation("Instance ID: {InstanceUID}", uid)` pattern consistently.

---

## LOW

### 20. `ConnectionManager.Close()` does not await `CloseAsync()`

**File:** [QsMessaging/RabbitMq/ConnectionManager.cs](QsMessaging/RabbitMq/ConnectionManager.cs#L23-L25)  
`conn.CloseAsync()` is called without `await`, suppressed with `#pragma warning disable CS4014`. Then `DisposeAsync()` is awaited, followed by a polling loop. This is fragile — `DisposeAsync` may throw if close hasn't completed.  
**Recommendation:** Await `CloseAsync()` before `DisposeAsync()`.

### 21. `NameGenerator` — typo `"unknowType"` should be `"unknownType"`

**File:** [QsMessaging/RabbitMq/NameGenerator.cs](QsMessaging/RabbitMq/NameGenerator.cs#L51)

```csharp
var fullName = type.FullName ?? "unknowType";
```

**Recommendation:** Fix typo to `"unknownType"`.

### 22. `ConsumerService` typo in error message: `"Ca'nt find methof"`

**File:** [QsMessaging/RabbitMq/Services/ConsumerService.cs](QsMessaging/RabbitMq/Services/ConsumerService.cs#L44)

```csharp
throw new NullReferenceException("Ca'nt find methof for consume model");
```

**Recommendation:** Fix to `"Cannot find method for consuming model"`. Also use `InvalidOperationException` instead of `NullReferenceException`.

### 23. `QueueService` has unused `using System.Xml.Linq`

**File:** [QsMessaging/RabbitMq/Services/QueueService.cs](QsMessaging/RabbitMq/Services/QueueService.cs#L7)  
**Recommendation:** Remove the unused import.

### 24. `QueueService` has unused variable `banseQueueName`

**File:** [QsMessaging/RabbitMq/NameGenerator.cs](QsMessaging/RabbitMq/NameGenerator.cs#L34)

```csharp
string banseQueueName = $"Qs:{TModel.FullName}";
```

This variable is declared but never used. Also contains a typo (`banse` → `base`).  
**Recommendation:** Remove the dead code.

### 25. `NullReferenceException` used as a logic exception

**Files:** [ConsumerService.cs](QsMessaging/RabbitMq/Services/ConsumerService.cs#L44), [ConsumerService.cs](QsMessaging/RabbitMq/Services/ConsumerService.cs#L73), [ConsumerService.cs](QsMessaging/RabbitMq/Services/ConsumerService.cs#L83)  
`NullReferenceException` is thrown manually in multiple places. This type is meant to be thrown by the runtime, not user code.  
**Recommendation:** Use `InvalidOperationException` or a custom exception type.

### 26. `ErrorConsumerType` enum has typo: `RecevingProblem`

**File:** [QsMessaging/Public/Handler/IQsMessagingConsumerErrorHandler.cs](QsMessaging/Public/Handler/IQsMessagingConsumerErrorHandler.cs)  
`RecevingProblem` should be `ReceivingProblem`. Since this is a public API type, changing it is a breaking change, but it should be corrected before a stable release.

### 27. Typo `IsResponsed` → should be `IsResponded`

**File:** [QsMessaging/RabbitMq/Models/StoreMessageRecord.cs](QsMessaging/RabbitMq/Models/StoreMessageRecord.cs)  
The property `IsResponsed` has a typo. This is internal, so safe to rename.

### 28. `NameGenerator.HashString` uses `SHA256.Create()` with explicit disposal — works but could use static method

**File:** [QsMessaging/RabbitMq/NameGenerator.cs](QsMessaging/RabbitMq/NameGenerator.cs#L59-L69)  
.NET 5+ offers `SHA256.HashData()` as a simpler, allocation-free alternative.  
**Recommendation:** Replace with `SHA256.HashData(inputBytes)`.

### 29. Unit test byte-by-byte assertion is fragile and unreadable

**File:** [QsMessagingUnitTests/SenderTest.cs](QsMessagingUnitTests/SenderTest.cs#L122-L139)  
`SendMessageCorrelationAsync_ShouldSendMessageWithCorrelationId` asserts the message body by checking individual byte values (`p.ToArray()[0] == 123`). This is fragile, unreadable, and breaks if serialization changes.  
**Recommendation:** Deserialize the body and assert on the object, or compare against the expected JSON string.

### 30. `ExchangeService` unconditionally adds to `storeExchangeRecords` — no deduplication

**File:** [QsMessaging/RabbitMq/Services/ExchangeService.cs](QsMessaging/RabbitMq/Services/ExchangeService.cs#L38)  
Unlike `QueueService` which checks for duplicates, `ExchangeService` adds a new `StoreExchangeRecord` on every call without checking if one already exists. This causes unbounded growth.  
**Recommendation:** Check for existing records before adding, consistent with `QueueService`.

---

## INFORMATIONAL / SUGGESTIONS

### 31. No `CancellationToken` propagation in several async methods

`SubscribeAsync`, `SubscribeHandlerAsync`, `GetOrCreateExchangeAsync`, `GetOrCreateQueuesAsync`, and `GetOrCreateConsumerAsync` do not accept or propagate `CancellationToken`. This makes graceful shutdown and timeout handling difficult.

### 32. Only fanout exchange type is supported

All exchanges are declared as `ExchangeType.Fanout`. This limits routing flexibility. Consider supporting Direct or Topic exchanges for targeted message delivery.

### 33. JSON serialization uses `System.Text.Json` with default options

No custom `JsonSerializerOptions` are configured. This means no support for polymorphism, custom naming policies, or handling of reference loops. Consider exposing serializer options through configuration.

### 34. Assembly scanning only covers entry assembly

`Assembly.GetEntryAssembly()!` is used to find handlers. Handlers in referenced libraries will not be discovered. Consider allowing users to specify additional assemblies to scan.

### 35. No health check support

There is no `IHealthCheck` implementation for monitoring RabbitMQ connectivity in ASP.NET Core applications.

### 36. Missing XML documentation on public API interfaces

The public interfaces (`IQsMessaging`, `IQsMessagingConfiguration`, handler interfaces) lack XML doc comments. For a NuGet package, IntelliSense documentation is important.

---

## Summary

| Severity      | Count  |
| ------------- | ------ |
| Critical      | 4      |
| High          | 6      |
| Medium        | 9      |
| Low           | 11     |
| Informational | 6      |
| **Total**     | **36** |
