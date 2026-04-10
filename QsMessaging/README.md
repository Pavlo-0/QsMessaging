# QsMessaging

QsMessaging supports both RabbitMQ and Azure Service Bus behind the same public API.

## RabbitMQ

```csharp
builder.Services.AddQsMessaging(options =>
{
    options.RabbitMQ.Host = "localhost";
});

var host = builder.Build();
await host.UseQsMessaging();
```

## Azure Service Bus

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

For the emulator, QsMessaging automatically uses management port `5300` for entity creation when `UseDevelopmentEmulator=true` is present.
