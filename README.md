# QsMessaging

**QsMessaging** is a .NET 8 library designed for sending and receiving messages between services or components of your application using **RabbitMQ** or **Azure Service Bus**. It supports horizontal scalability, allowing multiple instances of the same service to handle messages efficiently.  
Available on NuGet for seamless integration:  
[![NuGet](https://img.shields.io/nuget/v/QsMessaging.svg)](https://www.nuget.org/packages/QsMessaging/)

Project website:  
https://pavlo-0.github.io/QsMessaging/

A simple, scalable messaging solution for distributed systems.

> **Note:** Azure Service Bus support is in an early, not fully tested or implemented state. The API is the same as for RabbitMQ, but some features may be missing or behave unexpectedly.

## Installation

Install the package using the following command:

```bash
dotnet add package QsMessaging
```

## Registering the Library

Registering the library is simple. Add the following two lines of code to your `Program.cs`:

```csharp
// Add QsMessaging (use the default configuration)...
builder.Services.AddQsMessaging(options => { });

...
await host.UseQsMessaging();
```

### Handler Discovery

By default, QsMessaging scans the entry assembly and the assembly that called `AddQsMessaging`. If your handlers live in separate class libraries, pass those assemblies explicitly:

```csharp
builder.Services.AddQsMessaging(options =>
{
    options.AssembliesToScan.Add(typeof(MyMessageHandler).Assembly);
});

// or
builder.Services.AddQsMessaging(options => { }, typeof(MyMessageHandler).Assembly);
```

When scan assemblies are explicitly configured and no QsMessaging consumer handlers are found, registration fails fast with an `InvalidOperationException`.

### Default Configuration

**RabbitMQ** (default transport)

- Host: `localhost`
- UserName: `guest`
- Password: `guest`
- Port: `5672`

### Custom RabbitMQ Configuration

```csharp
using Polly;

builder.Services.AddQsMessaging(options =>
{
    options.HandlerResilience.MaxRetryAttempts = 1;
    options.HandlerResilience.Delay = TimeSpan.FromSeconds(1);
    options.Resilience.MaxRetryAttempts = 3;
    options.Resilience.Delay = TimeSpan.FromSeconds(1);
    options.Resilience.BackoffType = DelayBackoffType.Constant;
    options.Resilience.UseJitter = false;
    options.RabbitMQ.Host = "my-rabbitmq-host";
    options.RabbitMQ.UserName = "myuser";
    options.RabbitMQ.Password = "mypassword";
    options.RabbitMQ.Port = 5672;
});
```

### Serialization

By default, QsMessaging uses `System.Text.Json` with default JSON options and publishes messages as `application/json`.

Configure shared JSON options and contract metadata on `options.Serialization`:

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

RabbitMQ publishes `ContentType`, `ContentEncoding`, `Type`, and `qs-contract-version` headers. Azure Service Bus publishes `ContentType`, `Subject`, and matching application properties.

### Send Resilience And Missing Receivers

`SendMessageAsync` does not run a queue/subscription existence check before every message. That keeps the hot send path fast and avoids a management call per publish.

If the transport reports that a message cannot be delivered because the receiver entity is missing, QsMessaging retries the send with a Polly resilience pipeline and then logs a warning instead of throwing.

`options.Resilience` is shared by RabbitMQ and Azure Service Bus send paths.

Shared resilience options:

| Property           | Default    | Description                                                             |
| ------------------ | ---------- | ----------------------------------------------------------------------- |
| `MaxRetryAttempts` | `3`        | Number of retry attempts after the initial failed send. Set `0` to skip retry. |
| `Delay`            | `1 second` | Base delay between retries.                                             |
| `BackoffType`      | `Constant` | Polly backoff type: `Constant`, `Linear`, or `Exponential`.             |
| `UseJitter`        | `false`    | Adds jitter to retry delays when enabled.                               |

RabbitMQ behavior:

- Durable message publishing uses publisher confirms/return tracking and `mandatory` publishing.
- If RabbitMQ returns a normal message as unroutable, QsMessaging retries according to `options.Resilience`.
- After retries are exhausted, the send is swallowed and a warning is logged.

Azure Service Bus behavior:

- QsMessaging does not check subscriptions before every topic send.
- If Azure Service Bus throws `ServiceBusException` with `MessagingEntityNotFound`, QsMessaging retries according to `options.Resilience`.
- After retries are exhausted, the send is swallowed and a warning is logged.
- Azure Service Bus usually accepts a send to an existing topic even when the topic has no subscriptions, so that specific case cannot be detected after send without a management check.

### Handler Resilience And Error Handlers

When a user handler throws, QsMessaging retries that handler before calling `IQsMessagingConsumerErrorHandler`.

Configure handler retry on the root options object:

```csharp
using Polly;

builder.Services.AddQsMessaging(options =>
{
    options.HandlerResilience.MaxRetryAttempts = 1;
    options.HandlerResilience.Delay = TimeSpan.FromSeconds(1);
    options.HandlerResilience.BackoffType = DelayBackoffType.Constant;
    options.HandlerResilience.UseJitter = false;
});
```

`HandlerResilience` uses the same option shape as send resilience:

| Property           | Default    | Description                                                                  |
| ------------------ | ---------- | ---------------------------------------------------------------------------- |
| `MaxRetryAttempts` | `1`        | Number of retry attempts after the initial failed handler call. Set `0` to skip retry. |
| `Delay`            | `1 second` | Base delay between retries.                                                  |
| `BackoffType`      | `Constant` | Polly backoff type: `Constant`, `Linear`, or `Exponential`.                  |
| `UseJitter`        | `false`    | Adds jitter to retry delays when enabled.                                    |

If a retry succeeds, QsMessaging does not call consumer error handlers. If all attempts fail, QsMessaging creates a `FailedMessageWrapper` and routes it to the configured failed-message sinks.
Receive, deserialization, and dispatch failures are reported as `ErrorConsumerType.ReceivingProblem`; the misspelled `RecevingProblem` member remains as a compatibility alias.

### Failed Message Handling

By default QsMessaging preserves the existing behavior: registered `IQsMessagingConsumerErrorHandler` implementations are called and no error queue message is written. You can independently enable or disable each sink:

```csharp
builder.Services.AddQsMessaging(options =>
{
    options.FailedMessageHandling.CallErrorHandlers = true;  // default
    options.FailedMessageHandling.SendToErrorQueue = true;   // default false
});
```

When `SendToErrorQueue` is enabled, QsMessaging sends the same `FailedMessageWrapper` passed through `ErrorConsumerDetail.FailedMessage` to a queue named from the consumed queue/entity. RabbitMQ uses `<originalQueueName>:Error`; Azure Service Bus uses `<originalQueueName>-Error`. Long or invalid transport names are normalized or hashed with the transport suffix kept at the end.

The wrapper includes the transport, original queue/entity, error queue, original exchange/topic when available, routing key/subject/reply-to/correlation/message metadata, original contract and handler types, original body and headers/application properties, handler attempt count, configured retry count, all captured handler exceptions, and UTC creation/send timestamps. If both sinks are enabled, QsMessaging attempts both; a failure in one sink is logged and does not stop the other sink.

### Transport Cleanup Helpers

For debug or local reset scenarios you can explicitly clean transport entities before starting consumers again:

```csharp
await host.CleanUpTransportation();
await host.FullCleanUpTransportation();
await host.UseQsMessaging();
```

- `CleanUpTransportation()` removes entities that QsMessaging can derive from the current app contracts.
- `FullCleanUpTransportation()` removes only QsMessaging-prefixed entities by default.
- RabbitMQ safe full cleanup deletes queues and non-reserved exchanges with the `Qs:` prefix.
- Azure Service Bus safe full cleanup deletes queues/topics with the `Qs-` prefix and subscriptions with the `Qs_` prefix.
- Set `options.AllowDangerousFullCleanup = true` only for isolated debug/test scopes when you intentionally want to delete every visible queue/topic/subscription/exchange in the configured transport scope.
- For RabbitMQ, full cleanup uses the Management HTTP API for the configured virtual host.
- RabbitMQ Management HTTP API calls use `Microsoft.Extensions.Http.Resilience` with the configured `Resilience` retry settings for transient HTTP failures.

RabbitMQ full cleanup configuration:

```csharp
builder.Services.AddQsMessaging(options =>
{
    options.RabbitMQ.Host = "localhost";
    options.RabbitMQ.VirtualHost = "/";
    options.RabbitMQ.ManagementPort = 15672;
    options.RabbitMQ.ManagementScheme = "http";
    // Dangerous: deletes every visible transport entity in the configured scope.
    // Leave false unless the scope is isolated and disposable.
    options.AllowDangerousFullCleanup = false;
});
```

---

## Azure Service Bus Support _(Early Preview)_

> Azure Service Bus support is available but is **not fully tested or implemented**. The public interface (`IQsMessaging`) is identical to RabbitMQ — no code changes are needed in your handlers or senders.

### Registering with Azure Service Bus

Set `options.Transport = QsMessagingTransport.AzureServiceBus` and supply a connection string:

```csharp
builder.Services.AddQsMessaging(options =>
{
    options.Transport = QsMessagingTransport.AzureServiceBus;
    options.AzureServiceBus.ConnectionString = "<your-connection-string>";
    options.Resilience.MaxRetryAttempts = 3;
    options.Resilience.Delay = TimeSpan.FromSeconds(1);
});

...
await host.UseQsMessaging();
```

### Configuration for Cloud (Azure)

Use your Azure Service Bus namespace connection string directly:

```csharp
builder.Services.AddQsMessaging(options =>
{
    options.Transport = QsMessagingTransport.AzureServiceBus;
    options.AzureServiceBus.ConnectionString =
        "Endpoint=sb://your-namespace.servicebus.windows.net/;" +
        "SharedAccessKeyName=RootManageSharedAccessKey;" +
        "SharedAccessKey=YOUR_KEY;";
});
```

### Configuration for Emulator (Local Development)

The [Azure Service Bus Emulator](https://learn.microsoft.com/en-us/azure/service-bus-messaging/overview-emulator) uses separate ports for AMQP (messaging) and the management API:

```csharp
builder.Services.AddQsMessaging(options =>
{
    options.Transport = QsMessagingTransport.AzureServiceBus;
    options.AzureServiceBus.ConnectionString =
        "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";
    options.AzureServiceBus.EmulatorAmqpPort = 5672;
    options.AzureServiceBus.EmulatorManagementPort = 5300;
});
```

| Property                         | Default      | Description                                                                                              |
| -------------------------------- | ------------ | -------------------------------------------------------------------------------------------------------- |
| `ConnectionString`               | _(required)_ | Azure Service Bus connection string (cloud or emulator)                                                  |
| `EmulatorAmqpPort`               | `5672`       | AMQP port for the local emulator. Ignored for cloud namespaces.                                          |
| `EmulatorManagementPort`         | `5300`       | Management/admin port for the local emulator. Ignored for cloud namespaces.                              |
| `AdministrationConnectionString` | `null`       | Optional separate connection string for admin operations. Falls back to `ConnectionString` when omitted. |
| `Resilience`                     | see above    | Polly-based retry settings used after send failures caused by missing receiver entities.                 |

## Usage

### Sending Messages

#### Contract

Define a message contract:

```csharp
public class RegularMessageContract
{
    public required string MyTextMessage { get; set; }
}
```

#### Sending a Message

Inject `IQsMessaging` into your class:

```csharp
public YourClass(IQsMessaging qsMessaging) {}
```

Then, use it to send a message:

```csharp
await qsMessaging.SendMessageAsync(new RegularMessageContract { MyTextMessage = "My message." });
```

### Handling Messages

To handle the message, create a handler:

```csharp
public class RegularMessageContractHandler : IQsMessageHandler<RegularMessageContract>
{
    public Task<bool> Consumer(RegularMessageContract contractModel)
    {
        // Process the message here
        return Task.FromResult(true);
    }
}
```

All handlers discovered by QsMessaging are registered in DI as **Transient**.
This means each message/request is handled by a fresh handler instance, and handlers have full support for constructor injection of your application services.

### What Happens If A Handler Throws An Exception?

If your handler throws an exception, QsMessaging catches it, retries the handler according to `options.HandlerResilience`, and only then calls error handlers if all attempts fail.

- The exception does **not** crash the consumer loop.
- The handler is retried according to `options.HandlerResilience`.
- If a retry succeeds, consumer error handlers are not called.
- If all handler attempts fail, QsMessaging creates a `FailedMessageWrapper`.
- The wrapper is forwarded to your custom error handler(s), an error queue, or both depending on `options.FailedMessageHandling`.

Current behavior by transport:

- **RabbitMQ**: consumers use automatic acknowledge mode, so the message is treated as acknowledged even if the handler fails.
- **Azure Service Bus**: after the handler pipeline finishes, QsMessaging completes the message, so it is not re-delivered automatically.

If you need alerting or custom logging after handler retries are exhausted, implement `IQsMessagingConsumerErrorHandler`. If you need a durable copy of the failed message, enable `options.FailedMessageHandling.SendToErrorQueue`.

#### Short Operational Notes

- **Queue/exchange naming**: RabbitMQ uses names like `Qs:{FullTypeName}:ex` for exchanges and `Qs:{FullTypeName}:permanent` for durable queues. Azure Service Bus uses `Qs-Queue-{FullTypeName}` and `Qs-Topic-{FullTypeName}`. Long names are hashed.
- **Send retry**: ordinary message sends retry only after the transport reports a missing/unroutable receiver. There is no per-message pre-check of queue or subscription existence.
- **Handler retry / failed messages**: handlers are retried with `HandlerResilience`; after attempts are exhausted, `FailedMessageHandling` can call error handlers, send a wrapper to the transport error queue (`:Error` for RabbitMQ, `-Error` for Azure Service Bus), or both.
- **Azure Service Bus TTL**: normal `SendMessageAsync` messages use a 14 day TTL. Events use a 60 second TTL.
- **Multiple instances of one consumer**: for `IQsMessageHandler<T>`, instances compete on one shared queue, so one message is processed by one instance. For `IQsEventHandler<T>`, each instance gets its own temporary queue/subscription, so every instance receives the event.
- **Unhappy path**: if a handler still throws after configured retries, the failed-message wrapper is routed according to `FailedMessageHandling`.
- **Request/response**: default timeout is `50000` ms. If no response arrives in time, the request fails with `TimeoutException`. Correlation ID is generated automatically per request as a new `Guid` string and copied to the response. Cancellation token is passed into transport operations, but timeout is the main response wait guard. Duplicate responses are not specially deduplicated by the library; late responses are ignored after the request is removed from the local store.

#### Example: Custom Error Handler

```csharp
public class MessagingErrorHandler : IQsMessagingConsumerErrorHandler
{
    private readonly ILogger<MessagingErrorHandler> _logger;

    public MessagingErrorHandler(ILogger<MessagingErrorHandler> logger)
    {
        _logger = logger;
    }

    public Task HandleErrorAsync(Exception exception, ErrorConsumerDetail detail)
    {
        _logger.LogError(
            exception,
            "Handler failed. Queue or entity: {QueueName}, Handler: {HandlerType}, Payload type: {PayloadType}",
            detail.QueueName,
            detail.HandlerType,
            detail.GenericType);

        // Add your own logic here:
        // - save to database
        // - send alert
        // - push to dead-letter queue
        // - trigger retry workflow

        return Task.CompletedTask;
    }
}
```

---

### Request/Response Pattern

You can also use the **Request/Response** pattern to send a request and await a response. This is useful when you need to communicate between services and expect a response.

#### Request/Response Contract

Define the request and response contracts:

```csharp
public class MyRequestContract
{
    public required string RequestMessage { get; set; }
}

public class MyResponseContract
{
    public required string ResponseMessage { get; set; }
}
```

#### Sending a Request and Receiving a Response

To send a request and await a response, use the `RequestResponse<TRequest, TResponse>`:

```csharp
public class MyService
{
    private readonly IQsMessaging _qsMessaging;

    public MyService(IQsMessaging qsMessaging)
    {
        _qsMessaging = qsMessaging;
    }

    public async Task<MyResponseContract> SendRequestAsync(MyRequestContract request)
    {
        var response = await _qsMessaging.SendRequestResponseAsync<MyRequestContract, MyResponseContract>(request);
        return response;
    }
}
```

#### Handling Requests

To handle requests, implement the `IQsRequestResponseHandler<TRequest, TResponse>` interface:

```csharp
public class MyRequestHandler : IQsRequestResponseHandler<MyRequestContract, MyResponseContract>
{
    public Task<MyResponseContract> Handle(MyRequestContract request)
    {
        // Process the request and create a response
        return Task.FromResult(new MyResponseContract { ResponseMessage = "Response to: " + request.RequestMessage });
    }
}
```

## Dependency Injection Examples

The examples below show how handlers can consume dependencies through constructor injection.

### 1) Message Handler with Injected Services

```csharp
public interface IOrderProcessor
{
    Task ProcessAsync(CreateOrderMessage message);
}

public class CreateOrderMessage
{
    public required string OrderId { get; set; }
}

public class CreateOrderMessageHandler : IQsMessageHandler<CreateOrderMessage>
{
    private readonly IOrderProcessor _orderProcessor;
    private readonly ILogger<CreateOrderMessageHandler> _logger;

    public CreateOrderMessageHandler(
        IOrderProcessor orderProcessor,
        ILogger<CreateOrderMessageHandler> logger)
    {
        _orderProcessor = orderProcessor;
        _logger = logger;
    }

    public async Task<bool> Consumer(CreateOrderMessage contractModel)
    {
        _logger.LogInformation("Processing order {OrderId}", contractModel.OrderId);
        await _orderProcessor.ProcessAsync(contractModel);
        return true;
    }
}
```

### 2) Request/Response Handler with Injected Repository

```csharp
public interface IUserRepository
{
    Task<UserDto?> GetByIdAsync(Guid id);
}

public class GetUserRequest
{
    public Guid UserId { get; set; }
}

public class GetUserResponse
{
    public string? Name { get; set; }
    public bool Found { get; set; }
}

public class GetUserHandler : IQsRequestResponseHandler<GetUserRequest, GetUserResponse>
{
    private readonly IUserRepository _userRepository;

    public GetUserHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<GetUserResponse> Handle(GetUserRequest request)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId);
        return new GetUserResponse
        {
            Found = user is not null,
            Name = user?.Name
        };
    }
}
```

---

## Documentation

For detailed documentation, visit the [QsMessaging Wiki](https://github.com/Pavlo-0/QsMessaging/wiki).

**That's all, folks!**
