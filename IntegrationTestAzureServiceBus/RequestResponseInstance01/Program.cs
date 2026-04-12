using QsMessaging.Public;
using RequestResponseInstance01;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();
var azureServiceBusConfiguration = builder.Configuration.GetSection("QsMessaging:AzureServiceBus");
builder.Services.AddQsMessaging(options =>
{
    options.Transport = QsMessagingTransport.AzureServiceBus;
    options.AzureServiceBus.ConnectionString =
        azureServiceBusConfiguration["ConnectionString"]
        ?? "Endpoint=sb://your-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=YOUR_KEY;";
    options.AzureServiceBus.AdministrationConnectionString = azureServiceBusConfiguration["AdministrationConnectionString"];
    options.AzureServiceBus.EmulatorAmqpPort =
        azureServiceBusConfiguration.GetValue<int?>("EmulatorAmqpPort")
        ?? options.AzureServiceBus.EmulatorAmqpPort;
    options.AzureServiceBus.EmulatorManagementPort =
        azureServiceBusConfiguration.GetValue<int?>("EmulatorManagementPort")
        ?? options.AzureServiceBus.EmulatorManagementPort;
});

var host = builder.Build();
await host.UseQsMessaging();
host.Run();
