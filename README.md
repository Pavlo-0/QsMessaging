# QsMessaging

**QsMessaging** is a .NET 8 library designed for sending and receiving messages between services or components of your application using **RabbitMQ**. It supports horizontal scalability, allowing multiple instances of the same service to handle messages efficiently.  
Available on NuGet for seamless integration:  
[![NuGet](https://img.shields.io/nuget/v/QsMessaging.svg)](https://www.nuget.org/packages/QsMessaging/)  

A simple, scalable messaging solution for distributed systems.

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

**RabbitMQ**  
- Host: `localhost`  
- UserName: `guest`  
- Password: `guest`  
- Port: `5672`

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

---

**That's all, folks!**

## Documentation

For detailed documentation, visit the [QsMessaging Wiki](https://github.com/Pavlo-0/QsMessaging/wiki).
