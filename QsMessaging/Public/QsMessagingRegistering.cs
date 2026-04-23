using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using QsMessaging.AzureServiceBus.Services.Interfaces;
using QsMessaging.AzureServiceBus;
using QsMessaging.AzureServiceBus.Services;
using QsMessaging.RabbitMq;
using QsMessaging.RabbitMq.Services;
using QsMessaging.RabbitMq.Services.Interfaces;
using QsMessaging.RabbitMq.Interfaces;
using QsMessaging.Shared.Interface;
using QsMessaging.Shared.Services.Interfaces;
using QsMessaging.Shared.Services;
using QsMessaging.Shared;
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

        /// <summary>
        /// Stops the current transport connection and removes messaging entities created by the configured QsMessaging transport.
        /// Intended for debug or local reset scenarios before opening the transport again.
        /// </summary>
        public static async Task<IHost> CleanUpTransportation(this IHost host, CancellationToken cancellationToken = default)
        {
            var manager = host.Services.GetRequiredService<IQsMessagingConnectionManager>();
            await manager.Close(cancellationToken);

            var cleaner = host.Services.GetRequiredService<IQsMessagingTransportCleaner>();
            await cleaner.CleanUp(cancellationToken);

            return host;
        }

        /// <summary>
        /// Stops the current transport connection and removes all messaging entities visible to the configured transport scope.
        /// Intended for debug or local reset scenarios.
        /// </summary>
        public static async Task<IHost> FullCleanUpTransportation(this IHost host, CancellationToken cancellationToken = default)
        {
            var manager = host.Services.GetRequiredService<IQsMessagingConnectionManager>();
            await manager.Close(cancellationToken);

            var cleaner = host.Services.GetRequiredService<IQsMessagingTransportFullCleaner>();
            await cleaner.FullCleanUp(cancellationToken);

            return host;
        }

        private static void RegisterTransportServices(IServiceCollection services, IQsMessagingConfiguration configuration)
        {
            services.AddTransient<IConsumerService, ConsumerService>();   

            switch (configuration.Transport)
            {
                case QsMessagingTransport.RabbitMq:

                    services.AddSingleton<IRqConnectionService, RbConnectionService>();
                    services.AddTransient<ISubscriber, RqSubscriber>();
                    services.AddTransient<IQsMessagingConnectionManager, RqConnectionManager>();
                    services.AddTransient<IQsMessagingTransportCleaner, RqTransportCleaner>();
                    services.AddTransient<IQsMessagingTransportFullCleaner, RqTransportFullCleaner>();

                    services.AddTransient<ISender, RqSender>();
                    services.AddTransient<IRqNameGenerator, RqNameGenerator>();
                    services.AddTransient<IRqManagementService, RqManagementService>();


                    services.AddTransient<IRqExchangeService, RqExchangeService>();
                    services.AddTransient<IRqChannelService, RqChannelService>();
                    services.AddTransient<IRqQueueService, RqQueueService>();
                    services.AddTransient<IRqConsumerService, RqConsumerService>();
                    break;

                case QsMessagingTransport.AzureServiceBus:
                    services.AddSingleton<IAsbConnectionService, AsbConnectionService>();
                    services.AddTransient<ISubscriber, AsbSubscriber>();
                    services.AddTransient<IQsMessagingConnectionManager, AsbConnectionManager>();
                    services.AddTransient<IQsMessagingTransportCleaner, AsbTransportCleaner>();
                    services.AddTransient<IQsMessagingTransportFullCleaner, AsbTransportFullCleaner>();


                    services.AddTransient<ISender, AsbSender>();
                    services.AddTransient<IAsbNameGeneratorService, AsbNameGenerator>();

                    services.AddTransient<IAsbQueueService, AsbQueueService>();
                    services.AddTransient<IAsbTopicService, AsbTopicService>();
                    services.AddTransient<IAsbTopicSubscriptionService, AsbTopicSubscriptionService>();

                    services.AddTransient<IAsbServiceBusProcessorService, AsbServiceBusProcessorService>();
                    services.AddTransient<IAsbConsumerService, AsbConsumerService>();
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
