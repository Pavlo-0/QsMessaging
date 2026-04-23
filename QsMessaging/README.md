# QsMessaging

QsMessaging supports both RabbitMQ and Azure Service Bus behind the same public API.

Project website: https://pavlo-0.github.io/QsMessaging/
## RabbitMQ

## RabbitMQ

```csharp
builder.Services.AddQsMessaging(options =>
{
    options.RabbitMQ.Host = "localhost";
});

var host = builder.Build();
await host.UseQsMessaging();
```

For debug or reset scenarios you can also trigger cleanup explicitly:

```csharp
await host.CleanUpTransportation();
await host.FullCleanUpTransportation();
await host.UseQsMessaging();
```

`FullCleanUpTransportation()` removes everything visible in the configured transport scope.
For RabbitMQ it uses the Management HTTP API of the configured virtual host.
```csharp
builder.Services.AddQsMessaging(options =>
{
    options.RabbitMQ.Host = "localhost";
});

var host = builder.Build();
await host.UseQsMessaging();
```

## Azure Service Bus

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

For cloud namespaces, keep the regular Azure Service Bus connection string in `appsettings.json`.
For the emulator, place the emulator connection string and emulator-only port settings in `appsettings.Development.json`.
QsMessaging automatically uses AMQP port `5672` for send/receive operations and management port `5300` for entity creation when `UseDevelopmentEmulator=true` is present.
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
For the emulator, place the emulator connection string and emulator-only port settings in `appsettings.Development.json`.
QsMessaging automatically uses AMQP port `5672` for send/receive operations and management port `5300` for entity creation when `UseDevelopmentEmulator=true` is present.
If your emulator uses a different AMQP port, set `options.AzureServiceBus.EmulatorAmqpPort`.
In this repository's local Docker setup, RabbitMQ uses host port `5672`, so the Service Bus emulator is published on host port `5673`.
If your emulator uses a different AMQP port, set `options.AzureServiceBus.EmulatorAmqpPort`.
In this repository's local Docker setup, RabbitMQ uses host port `5672`, so the Service Bus emulator is published on host port `5673`.
