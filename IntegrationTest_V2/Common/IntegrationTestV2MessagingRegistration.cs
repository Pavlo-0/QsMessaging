using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QsMessaging.Public;

namespace IntegrationTestV2.Common;

public static class IntegrationTestV2MessagingRegistration
{
    public static IServiceCollection AddIntegrationTestV2Messaging(
        this IServiceCollection services,
        IConfiguration configuration,
        Assembly handlerAssembly)
    {
        var settings = new IntegrationTestV2MessagingSettings();
        configuration.GetSection("QsMessaging").Bind(settings);

        services.AddQsMessaging(options =>
        {
            options.Transport = QsMessagingTransport.RabbitMq;
            options.AssembliesToScan.Add(handlerAssembly);
            options.RequestResponseTimeout = settings.RequestResponseTimeoutMilliseconds;

            options.RabbitMQ.Host = settings.RabbitMQ.Host;
            options.RabbitMQ.UserName = settings.RabbitMQ.UserName;
            options.RabbitMQ.Password = settings.RabbitMQ.Password;
            options.RabbitMQ.Port = settings.RabbitMQ.Port;
            options.RabbitMQ.VirtualHost = settings.RabbitMQ.VirtualHost;
        });

        return services;
    }
}

internal sealed class IntegrationTestV2MessagingSettings
{
    public int RequestResponseTimeoutMilliseconds { get; set; } = 15_000;

    public IntegrationTestV2RabbitMqSettings RabbitMQ { get; set; } = new();
}

internal sealed class IntegrationTestV2RabbitMqSettings
{
    public string Host { get; set; } = "localhost";

    public string UserName { get; set; } = "guest";

    public string Password { get; set; } = "guest";

    public int Port { get; set; } = 5672;

    public string VirtualHost { get; set; } = "/";
}
