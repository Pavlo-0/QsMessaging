using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QsMessaging.Public;

namespace IntegrationTest.Common;

internal static class IntegrationTestQsMessagingRegistration
{
    public static IServiceCollection AddConfiguredQsMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = new IntegrationTestQsMessagingSettings();
        configuration.GetSection("QsMessaging").Bind(settings);

        services.AddQsMessaging(options =>
        {
            options.Transport = settings.Transport;

            options.RabbitMQ.Host = settings.RabbitMQ.Host;
            options.RabbitMQ.UserName = settings.RabbitMQ.UserName;
            options.RabbitMQ.Password = settings.RabbitMQ.Password;
            options.RabbitMQ.Port = settings.RabbitMQ.Port;
            options.RabbitMQ.VirtualHost = settings.RabbitMQ.VirtualHost;
            options.RabbitMQ.ManagementScheme = settings.RabbitMQ.ManagementScheme;
            options.RabbitMQ.ManagementPort = settings.RabbitMQ.ManagementPort;
            options.RabbitMQ.ManagementApiBaseAddress = settings.RabbitMQ.ManagementApiBaseAddress;
            options.RabbitMQ.ManagementUserName = settings.RabbitMQ.ManagementUserName;
            options.RabbitMQ.ManagementPassword = settings.RabbitMQ.ManagementPassword;

            options.AzureServiceBus.ConnectionString = settings.AzureServiceBus.ConnectionString;
            options.AzureServiceBus.AdministrationConnectionString = settings.AzureServiceBus.AdministrationConnectionString;
            options.AzureServiceBus.EmulatorAmqpPort = settings.AzureServiceBus.EmulatorAmqpPort;
            options.AzureServiceBus.EmulatorManagementPort = settings.AzureServiceBus.EmulatorManagementPort;
        });

        return services;
    }
}

internal sealed class IntegrationTestQsMessagingSettings
{
    public QsMessagingTransport Transport { get; set; } = QsMessagingTransport.RabbitMq;

    public IntegrationTestRabbitMqSettings RabbitMQ { get; set; } = new();

    public IntegrationTestAzureServiceBusSettings AzureServiceBus { get; set; } = new();
}

internal sealed class IntegrationTestRabbitMqSettings
{
    public string Host { get; set; } = "localhost";

    public string UserName { get; set; } = "guest";

    public string Password { get; set; } = "guest";

    public int Port { get; set; } = 5672;

    public string VirtualHost { get; set; } = "/";

    public string ManagementScheme { get; set; } = "http";

    public int ManagementPort { get; set; } = 15672;

    public string? ManagementApiBaseAddress { get; set; }

    public string? ManagementUserName { get; set; }

    public string? ManagementPassword { get; set; }
}

internal sealed class IntegrationTestAzureServiceBusSettings
{
    public string ConnectionString { get; set; }
        = "Endpoint=sb://your-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=YOUR_KEY;";

    public string? AdministrationConnectionString { get; set; }

    public int EmulatorAmqpPort { get; set; } = 5673;

    public int EmulatorManagementPort { get; set; } = 5300;
}
