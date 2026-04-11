# QsMessaging

**QsMessaging** is a .NET 8 library for message-based communication between console or worker applications.
It supports both **RabbitMQ** and **Azure Service Bus** behind the same public API.

[![NuGet](https://img.shields.io/nuget/v/QsMessaging.svg)](https://www.nuget.org/packages/QsMessaging/)

## Installation

```bash
dotnet add package QsMessaging
```

## Registration

Register QsMessaging in `Program.cs`, then start it once after the host is built.

### RabbitMQ

RabbitMQ remains the default transport.

```csharp
builder.Services.AddQsMessaging(options =>
{
    options.RabbitMQ.Host = "localhost";
    options.RabbitMQ.UserName = "guest";
    options.RabbitMQ.Password = "guest";
    options.RabbitMQ.Port = 5672;
});

var host = builder.Build();
await host.UseQsMessaging();
```

### Azure Service Bus

```csharp
builder.Services.AddQsMessaging(options =>
{
    options.Transport = QsMessagingTransport.AzureServiceBus;
    options.AzureServiceBus.ConnectionString =
        "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";
});

var host = builder.Build();
await host.UseQsMessaging();
```

For cloud namespaces, keep the regular Azure Service Bus connection string in `appsettings.json`.
For the Azure Service Bus emulator, place the emulator connection string and emulator-only port settings in `appsettings.Development.json`.
QsMessaging automatically derives the AMQP endpoint on port `5672` and the management endpoint on port `5300` when `UseDevelopmentEmulator=true` is present and the endpoint has no port.
If your emulator uses a different AMQP port, set `options.AzureServiceBus.EmulatorAmqpPort`.
If your emulator uses a different management endpoint, set `options.AzureServiceBus.AdministrationConnectionString` explicitly.
In this repository's local Docker setup, RabbitMQ uses host port `5672`, so the Service Bus emulator is published on host port `5673`.

## Sending Messages

```csharp
public class RegularMessageContract
{
    public required string MyTextMessage { get; set; }
}

public class MyService(IQsMessaging qsMessaging)
{
    public Task SendAsync()
    {
        return qsMessaging.SendMessageAsync(
            new RegularMessageContract { MyTextMessage = "Hello" });
    }
}
```

## Handling Messages

```csharp
public class RegularMessageContractHandler : IQsMessageHandler<RegularMessageContract>
{
    public Task Consumer(RegularMessageContract contractModel)
    {
        return Task.CompletedTask;
    }
}
```

Handlers discovered by QsMessaging are registered automatically as transient services, so constructor injection works normally.

## Request/Response

```csharp
public class MyRequest
{
    public required string RequestMessage { get; set; }
}

public class MyResponse
{
    public required string ResponseMessage { get; set; }
}

public class RequestClient(IQsMessaging qsMessaging)
{
    public Task<MyResponse> SendAsync(MyRequest request)
    {
        return qsMessaging.RequestResponse<MyRequest, MyResponse>(request);
    }
}
```

```csharp
public class MyRequestHandler : IQsRequestResponseHandler<MyRequest, MyResponse>
{
    public Task<MyResponse> Consumer(MyRequest request)
    {
        return Task.FromResult(new MyResponse
        {
            ResponseMessage = $"Response to: {request.RequestMessage}"
        });
    }
}
```

## Event Handlers

```csharp
public class OrderCreatedEvent
{
    public required string OrderId { get; set; }
}

public class OrderCreatedHandler : IQsEventHandler<OrderCreatedEvent>
{
    public Task Consumer(OrderCreatedEvent contract)
    {
        return Task.CompletedTask;
    }
}
```

## Notes

- `IQsMessaging` stays the same for both transports.
- RabbitMQ is still the default transport.
- Azure Service Bus requires a namespace-level connection string with management permissions because QsMessaging creates queues, topics, and subscriptions automatically.
- The package examples in this repository still demonstrate the general handler and messaging patterns and can be adapted for either transport.
