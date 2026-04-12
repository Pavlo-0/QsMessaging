# QsMessaging

**QsMessaging** is a .NET 8 library designed for sending and receiving messages between services or components of your application using **RabbitMQ** or **Azure Service Bus**. It supports horizontal scalability, allowing multiple instances of the same service to handle messages efficiently.  
Available on NuGet for seamless integration:  
[![NuGet](https://img.shields.io/nuget/v/QsMessaging.svg)](https://www.nuget.org/packages/QsMessaging/)

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

### Default Configuration

**RabbitMQ** (default transport)

- Host: `localhost`
- UserName: `guest`
- Password: `guest`
- Port: `5672`

### Custom RabbitMQ Configuration

```csharp
builder.Services.AddQsMessaging(options =>
{
    options.RabbitMQ.Host = "my-rabbitmq-host";
    options.RabbitMQ.UserName = "myuser";
    options.RabbitMQ.Password = "mypassword";
    options.RabbitMQ.Port = 5672;
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

If your handler throws an exception, QsMessaging catches it and passes it to every registered `IQsMessagingConsumerErrorHandler` implementation.

- The exception does **not** crash the consumer loop.
- The exception is forwarded to your custom error handler(s) together with message metadata.
- The message is **not retried automatically** by QsMessaging.

Current behavior by transport:

- **RabbitMQ**: consumers use automatic acknowledge mode, so the message is treated as acknowledged even if the handler fails.
- **Azure Service Bus**: after the handler pipeline finishes, QsMessaging completes the message, so it is not re-delivered automatically.

If you need retry, dead-letter, alerting, or custom logging, implement `IQsMessagingConsumerErrorHandler` and handle the exception there.

#### Short Operational Notes

- **Queue/exchange naming**: RabbitMQ uses names like `Qs:{FullTypeName}:ex` for exchanges and `Qs:{FullTypeName}:permanent` for durable queues. Azure Service Bus uses `Qs-Queue-{FullTypeName}` and `Qs-Topic-{FullTypeName}`. Long names are hashed.
- **Retry / dead-letter**: there is currently no built-in retry or dead-letter flow managed by QsMessaging.
- **Multiple instances of one consumer**: for `IQsMessageHandler<T>`, instances compete on one shared queue, so one message is processed by one instance. For `IQsEventHandler<T>`, each instance gets its own temporary queue/subscription, so every instance receives the event.
- **Unhappy path**: if a handler throws, the exception is sent to `IQsMessagingConsumerErrorHandler`, but the message is not automatically retried by the library.
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
