# QsMessaging

QsMessaging supports RabbitMQ and Azure Service Bus behind the same public API for typed messages, events, and request/response flows.

Project website: https://pavlo-0.github.io/QsMessaging/

## RabbitMQ

RabbitMQ is the default transport.

```csharp
using Polly;

builder.Services.AddQsMessaging(options =>
{
    options.RabbitMQ.Host = "localhost";
    options.RabbitMQ.Resilience.MaxRetryAttempts = 3;
    options.RabbitMQ.Resilience.Delay = TimeSpan.FromSeconds(1);
    options.RabbitMQ.Resilience.BackoffType = DelayBackoffType.Constant;
});

var host = builder.Build();
await host.UseQsMessaging();
```

## Azure Service Bus

Azure Service Bus support is available as an early preview behind the same public API.

```csharp
builder.Services.AddQsMessaging(options =>
{
    options.Transport = QsMessagingTransport.AzureServiceBus;
    options.AzureServiceBus.ConnectionString =
        "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";
    options.AzureServiceBus.Resilience.MaxRetryAttempts = 3;
    options.AzureServiceBus.Resilience.Delay = TimeSpan.FromSeconds(1);
});

var host = builder.Build();
await host.UseQsMessaging();
```

For cloud namespaces, use the regular Azure Service Bus namespace connection string. For the emulator, keep emulator-only port settings in development configuration.

## Send Resilience

QsMessaging does not check receiver queues or subscriptions before every `SendMessageAsync` call. The send path stays fast and retry starts only after the transport reports a missing/unroutable receiver.

Resilience settings are available under both `options.RabbitMQ.Resilience` and `options.AzureServiceBus.Resilience`:

| Property           | Default    | Description                                                             |
| ------------------ | ---------- | ----------------------------------------------------------------------- |
| `MaxRetryAttempts` | `3`        | Number of retry attempts after the initial failed send. Set `0` to skip retry. |
| `Delay`            | `1 second` | Base delay between retries.                                             |
| `BackoffType`      | `Constant` | Polly backoff type: `Constant`, `Linear`, or `Exponential`.             |
| `UseJitter`        | `false`    | Adds jitter to retry delays when enabled.                               |

- RabbitMQ durable messages use publisher confirms/return tracking with `mandatory` publishing. If RabbitMQ returns a normal message as unroutable, QsMessaging retries with Polly, then logs a warning and swallows the failure.
- RabbitMQ Management HTTP API calls use `Microsoft.Extensions.Http.Resilience` with `options.RabbitMQ.Resilience` for transient HTTP failures.
- Azure Service Bus retries only when send throws `ServiceBusException` with `MessagingEntityNotFound`. A send to an existing topic with no subscriptions is usually accepted by Azure Service Bus, so that case cannot be detected after send without a management check.
- Azure Service Bus normal `SendMessageAsync` messages use a 14 day TTL. Events use a 60 second TTL.

## Cleanup Helpers

For debug or reset scenarios you can explicitly clean transport entities:

```csharp
await host.CleanUpTransportation();
await host.FullCleanUpTransportation();
await host.UseQsMessaging();
```

`FullCleanUpTransportation()` removes everything visible in the configured transport scope. For RabbitMQ it uses the Management HTTP API of the configured virtual host.
