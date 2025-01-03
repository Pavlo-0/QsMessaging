﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using QsMessaging.RabbitMq;
using QsMessaging.RabbitMq.Interface;
using QsMessaging.RabbitMq.Interfaces;
using QsMessaging.RabbitMq.Services;
using QsMessaging.RabbitMq.Services.Interfaces;
using System.Reflection;

namespace QsMessaging.Public
{
    public static class QsMessagingRegistering
    {
        public static IServiceCollection AddQsMessaging(this IServiceCollection services, Action<IQsMessagingConfiguration> options)
        {
            var configuration = new Configuration();
            options(configuration);

            services.AddSingleton<IQsMessagingConfiguration>(configuration);

            services.AddTransient<IInstanceService, InstanceService>();
            services.AddTransient<IQsMessaging, QsMessagingGate>();
            services.AddTransient<IQsMessagingConnectionManager, ConnectionManager>();

            services.AddTransient<ISubscriber, Subscriber>();
            services.AddTransient<IRabbitMqSender, Sender>();
            services.AddTransient<ISender, Sender>();

            services.AddTransient<INameGenerator, NameGenerator>();

            services.AddSingleton<IConnectionService, ConnectionService>();
            services.AddTransient<IExchangeService, ExchangeService>();
            services.AddTransient<IChannelService, ChannelService>();
            services.AddTransient<IExchangeService, ExchangeService>();
            services.AddTransient<IQueueService, QueueService>();
            var handlerGeneratorInstance = new HandlerService(services, Assembly.GetEntryAssembly()!);
            services.AddTransient<IHandlerService>(hg=>
            {
                return handlerGeneratorInstance;
            });

            handlerGeneratorInstance.RegisterAllHandlers();
            services.AddTransient<IConsumerService, ConsumerService>();

            services.AddTransient<IRequestResponseMessageStore , RequestResponseMessageStore>();

            services.AddTransient(typeof(Lazy<>), typeof(LazyService<>));

            return services;
        }

        public static async Task<IHost> UseQsMessaging(this IHost host)
        {
            var connectionStorage = host.Services.GetRequiredService<IConnectionService>();
            var connection = await connectionStorage.GetOrCreateConnectionAsync();

            var subscriber = host.Services.GetRequiredService<ISubscriber>();
            await subscriber.SubscribeAsync();
           
            return host;
        }
    }
}
