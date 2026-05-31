# QsMessaging

QsMessaging supports RabbitMQ and Azure Service Bus behind the same public API for typed messages, events, and request/response flows.

Project website: https://pavlo-0.github.io/QsMessaging/

## RabbitMQ

RabbitMQ is the default transport.

```csharp
using Polly;

builder.Services.AddQsMessaging(options =>
{
    options.HandlerResilience.MaxRetryAttempts = 1;
    options.HandlerResilience.Delay = TimeSpan.FromSeconds(1);
    options.Resilience.MaxRetryAttempts = 3;
    options.Resilience.Delay = TimeSpan.FromSeconds(1);
    options.Resilience.BackoffType = DelayBackoffType.Constant;
    options.RabbitMQ.Host = "localhost";
});

var host = builder.Build();
await host.UseQsMessaging();
```

## Handler Discovery

By default, QsMessaging scans the entry assembly and the assembly that called `AddQsMessaging`. If handlers live in separate class libraries, pass those assemblies explicitly:

```csharp
builder.Services.AddQsMessaging(options =>
{
    options.AssembliesToScan.Add(typeof(MyMessageHandler).Assembly);
});

// or
builder.Services.AddQsMessaging(options => { }, typeof(MyMessageHandler).Assembly);
```

When scan assemblies are explicitly configured and no QsMessaging consumer handlers are found, registration fails fast with an `InvalidOperationException`.

## Azure Service Bus

Azure Service Bus support is available as an early preview behind the same public API.

```csharp
builder.Services.AddQsMessaging(options =>
{
    options.Transport = QsMessagingTransport.AzureServiceBus;
    options.AzureServiceBus.ConnectionString =
        "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";
    options.Resilience.MaxRetryAttempts = 3;
    options.Resilience.Delay = TimeSpan.FromSeconds(1);
});

var host = builder.Build();
await host.UseQsMessaging();
```

For cloud namespaces, use the regular Azure Service Bus namespace connection string. For the emulator, keep emulator-only port settings in development configuration.

## Serialization

Configure shared `System.Text.Json` options and contract metadata on `options.Serialization`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

builder.Services.AddQsMessaging(options =>
{
    options.Serialization.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.Serialization.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.Serialization.ContractVersion = "v2";
});
```

RabbitMQ publishes JSON content-type, content-encoding, contract type, and contract version headers. Azure Service Bus publishes the same contract metadata in `ContentType`, `Subject`, and application properties.

## Send Resilience

QsMessaging does not check receiver queues or subscriptions before every `SendMessageAsync` call. The send path stays fast and retry starts only after the transport reports a missing/unroutable receiver.

Send resilience settings are shared by both transports under `options.Resilience`:

| Property           | Default    | Description                                                             |
| ------------------ | ---------- | ----------------------------------------------------------------------- |
| `MaxRetryAttempts` | `3`        | Number of retry attempts after the initial failed send. Set `0` to skip retry. |
| `Delay`            | `1 second` | Base delay between retries.                                             |
| `BackoffType`      | `Constant` | Polly backoff type: `Constant`, `Linear`, or `Exponential`.             |
| `UseJitter`        | `false`    | Adds jitter to retry delays when enabled.                               |

- RabbitMQ durable messages use publisher confirms/return tracking with `mandatory` publishing. If RabbitMQ returns a normal message as unroutable, QsMessaging retries with Polly, then logs a warning and swallows the failure.
- RabbitMQ Management HTTP API calls use `Microsoft.Extensions.Http.Resilience` with `options.Resilience` for transient HTTP failures.
- Azure Service Bus retries only when send throws `ServiceBusException` with `MessagingEntityNotFound`. A send to an existing topic with no subscriptions is usually accepted by Azure Service Bus, so that case cannot be detected after send without a management check.
- Azure Service Bus normal `SendMessageAsync` messages use a 14 day TTL. Events use a 60 second TTL.

## Handler Resilience

When a user handler throws, QsMessaging retries that handler before calling `IQsMessagingConsumerErrorHandler`.

`options.HandlerResilience` uses the same option shape as send resilience:

| Property           | Default    | Description                                                                  |
| ------------------ | ---------- | ---------------------------------------------------------------------------- |
| `MaxRetryAttempts` | `1`        | Number of retry attempts after the initial failed handler call. Set `0` to skip retry. |
| `Delay`            | `1 second` | Base delay between retries.                                                  |
| `BackoffType`      | `Constant` | Polly backoff type: `Constant`, `Linear`, or `Exponential`.                  |
| `UseJitter`        | `false`    | Adds jitter to retry delays when enabled.                                    |

If a retry succeeds, error handlers are not called. If all attempts fail, QsMessaging creates a `FailedMessageWrapper` and routes it to the configured failed-message sinks.
Receive, deserialization, and dispatch failures are reported as `ErrorConsumerType.ReceivingProblem`; the misspelled `RecevingProblem` member remains as a compatibility alias.

## Failed Message Handling

Existing behavior is preserved by default: registered `IQsMessagingConsumerErrorHandler` implementations are called and no error queue message is written. Configure each sink independently:

```csharp
builder.Services.AddQsMessaging(options =>
{
    options.FailedMessageHandling.CallErrorHandlers = true;  // default
    options.FailedMessageHandling.SendToErrorQueue = true;   // default false
});
```

When `SendToErrorQueue` is enabled, QsMessaging serializes the same `FailedMessageWrapper` exposed on `ErrorConsumerDetail.FailedMessage`. RabbitMQ sends it to `<originalQueueName>:Error`; Azure Service Bus sends it to `<originalQueueName>-Error`. Names are normalized or hashed when required by the transport, with the transport suffix kept at the end.

The wrapper includes transport name, source queue/entity, error queue, exchange/topic when available, routing key/subject/reply-to/correlation/message metadata, original contract and handler types, original body and headers/application properties, handler attempt count, configured retry count, captured exceptions, and UTC creation/send timestamps. If both sinks are enabled, QsMessaging attempts both and logs sink failures independently.

## Cleanup Helpers

For debug or reset scenarios you can explicitly clean transport entities:

```csharp
await host.CleanUpTransportation();
await host.FullCleanUpTransportation();
await host.UseQsMessaging();
```

`FullCleanUpTransportation()` removes only QsMessaging-prefixed entities by default: RabbitMQ queues/exchanges with `Qs:`, Azure Service Bus queues/topics with `Qs-`, and Azure subscriptions with `Qs_`. Set `options.AllowDangerousFullCleanup = true` only for isolated debug/test scopes when you intentionally want to delete every visible transport entity in the configured scope.
