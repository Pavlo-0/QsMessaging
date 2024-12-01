using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using QsMessaging.RabbitMq;
using QsMessaging.RabbitMq.Interface;
using QsMessaging.RabbitMq.Interfaces;
using QsMessaging.RabbitMq.Services;
using QsMessaging.RabbitMq.Services.Interfaces;
using System.Collections.Concurrent;
using System.Reflection;

namespace QsMessaging.Public
{
    public static class QsMessagingRegistering
    {
        public static IServiceCollection AddQsMessaging(this IServiceCollection services, Action<QsMessagingConfiguration> options)
        {
            var configuration = new QsMessagingConfiguration();
            options(configuration);

            services.AddTransient<IInstanceService, InstanceService>();
            services.AddTransient<IQsMessaging, QsMessagingGate>();
            services.AddTransient<IQsMessagingConnectionManager, ConnectionManager>();

            services.AddTransient<ISubscriber, Subscriber>();
            services.AddTransient<IRabbitMqSender, Sender>();
            services.AddTransient<ISender, Sender>();

            services.AddTransient<INameGenerator, NameGenerator>();

            services.AddSingleton<IConnectionService>(sp =>
            {
                return new ConnectionService(configuration);
            });
            services.AddTransient<IExchangeService, ExchangeService>();
            services.AddSingleton<IChannelService, ChannelService>();
            services.AddSingleton<IExchangeService, ExchangeService>();
            services.AddSingleton<IQueueService, QueueService>();
            var handlerGeneratorInstance = new HandlerService(services, Assembly.GetEntryAssembly()!);
            services.AddSingleton<IHandlerService>(hg=>
            {
                return handlerGeneratorInstance;
            });

            handlerGeneratorInstance.RegisterAllHandlers(services);
            services.AddSingleton<IConsumerService, ConsumerService>();

            services.AddSingleton<IRequestResponseMessageStore , RequestResponseMessageStore>();

            return services;
        }

        public static async Task<IHost> UseQsMessaging(this IHost host)
        {
            var connectionStorage = host.Services.GetRequiredService<IConnectionService>();
            var connection = await connectionStorage.GetOrCreateConnectionAsync();

            var subscriber = host.Services.GetRequiredService<ISubscriber>();
            await subscriber.Subscribe();
           
            return host;
        }
    }
}
