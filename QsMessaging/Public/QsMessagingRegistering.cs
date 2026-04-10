using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using QsMessaging.RabbitMq;
using QsMessaging.RabbitMq.Interface;
using QsMessaging.RabbitMq.Interfaces;
using QsMessaging.RabbitMq.Services;
using QsMessaging.RabbitMq.Services.Interfaces;
using QsMessaging.AzureServiceBus.Services.Interfaces;
using QsMessaging.Transporting;
using QsMessaging.Transporting.Interfaces;
using System.Reflection;

namespace QsMessaging.Public
{
    public static class QsMessagingRegistering
    {
        public static IServiceCollection AddQsMessaging(this IServiceCollection services, Action<IQsMessagingConfiguration> options)
        {
            var configuration = new Configuration();
            options(configuration);
            ValidateConfiguration(configuration);

            services.AddSingleton<IQsMessagingConfiguration>(configuration);

            services.AddTransient<IInstanceService, InstanceService>();
            services.AddTransient<IQsMessaging, QsMessagingGate>();
            services.AddTransient<INameGenerator, NameGenerator>();
            var handlerGeneratorInstance = new HandlerService(services, Assembly.GetEntryAssembly()!);
            services.AddTransient<IHandlerService>(hg=>
            {
                return handlerGeneratorInstance;
            });

            handlerGeneratorInstance.RegisterAllHandlers();
            services.AddTransient<IRequestResponseMessageStore , RequestResponseMessageStore>();

            services.AddTransient(typeof(Lazy<>), typeof(LazyService<>));
            RegisterTransportServices(services, configuration);

            return services;
        }

        public static async Task<IHost> UseQsMessaging(this IHost host)
        {
            var manager = host.Services.GetRequiredService<IQsMessagingConnectionManager>();
            await manager.Open();
           
            return host;
        }

        private static void RegisterTransportServices(IServiceCollection services, IQsMessagingConfiguration configuration)
        {
            switch (configuration.Transport)
            {
                case QsMessagingTransport.RabbitMq:
                    services.AddTransient<IQsMessagingConnectionManager, ConnectionManager>();
                    services.AddTransient<ISubscriber, Subscriber>();
                    services.AddTransient<IRabbitMqSender, Sender>();
                    services.AddTransient<ISender, Sender>();
                    services.AddTransient<ITransportSender, RabbitMqTransportSenderAdapter>();

                    services.AddSingleton<IConnectionService, ConnectionService>();
                    services.AddTransient<IExchangeService, ExchangeService>();
                    services.AddTransient<IChannelService, ChannelService>();
                    services.AddTransient<IExchangeService, ExchangeService>();
                    services.AddTransient<IQueueService, QueueService>();
                    services.AddTransient<IConsumerService, ConsumerService>();
                    break;

                case QsMessagingTransport.AzureServiceBus:
                    services.AddSingleton<IQsMessagingConnectionManager, AzureServiceBus.ConnectionManager>();
                    services.AddSingleton<AzureServiceBus.Sender>();
                    services.AddSingleton<ITransportSender>(sp => sp.GetRequiredService<AzureServiceBus.Sender>());
                    services.AddSingleton<AzureServiceBus.IAzureServiceBusResponseSender>(sp => sp.GetRequiredService<AzureServiceBus.Sender>());
                    services.AddSingleton<IClientService, AzureServiceBus.Services.ClientService>();
                    services.AddSingleton<IAdministrationService, AzureServiceBus.Services.AdministrationService>();
                    services.AddSingleton<AzureServiceBus.IAzureServiceBusSubscriber, AzureServiceBus.Subscriber>();
                    break;

                default:
                    throw new NotSupportedException($"The transport '{configuration.Transport}' is not supported.");
            }
        }

        private static void ValidateConfiguration(IQsMessagingConfiguration configuration)
        {
            if (configuration.Transport != QsMessagingTransport.AzureServiceBus)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(configuration.AzureServiceBus.ConnectionString))
            {
                throw new InvalidOperationException("Azure Service Bus transport requires AzureServiceBus.ConnectionString to be configured.");
            }
        }
    }
}
