using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using QsMessaging.Public.Handler;
using QsMessaging.RabbitMq;
using QsMessaging.RabbitMq.Interface;
using QsMessaging.RabbitMq.Services;
using QsMessaging.RabbitMq.Services.Interfaces;
using System.Collections.Concurrent;
using System.Reflection;

namespace QsMessaging.Public
{
    public static class QsMessagingRegistering
    {
        private record HandlersStoreRecord(Type InterfaceType, Type ImplementationType, Type GenericType);
            //private static readonly Dictionary<string, object> _messagesTyping = new Dictionary<string, object>();
            private static readonly ConcurrentBag<HandlersStoreRecord> _MessageHandlersStore = new ConcurrentBag<HandlersStoreRecord>();

        public static IServiceCollection AddQsMessaging(this IServiceCollection services, Action<QsMessagingConfiguration> options)
        {
            var configuration = new QsMessagingConfiguration();
            options(configuration);

            services.AddTransient<IQsMessaging, QsMessagingGate>();
            services.AddTransient<IRabbitMqSender, Sender>();
            services.AddTransient<IExchangeNameGenerator, ExchangeNameGenerator>();
            services.AddTransient<IQueuesGenerator, QueuesGenerator>();

            services.AddSingleton<IRabbitMqConnectionStorage>(sp =>
            {
                return new RabbitMqConnectionStorage(configuration);
            });

            //Handler part
            RegisterMessageHandlers(services, Assembly.GetEntryAssembly());
            services.AddTransient<IRabbitMqSubscriber, RabbitMqSubscriber>();

            return services;
        }

        public static async Task<IHost> UseQsMessaging(this IHost host)
        {
            var connectionStorage = host.Services.GetRequiredService<IRabbitMqConnectionStorage>();
            var connection = connectionStorage.GetConnectionAsync();

            var subscriber = host.Services.GetRequiredService<IRabbitMqSubscriber>();

            foreach (var record in _MessageHandlersStore)
            {
                await subscriber.SubscribeAsync(record.InterfaceType, record.ImplementationType, record.GenericType);
                //subscriber.SubscribeAsync(record.InterfaceType, record.GenericType);
            }
            

            await connection;

            return host;
        }

        public static IServiceCollection RegisterMessageHandlers(this IServiceCollection services, Assembly assembly)
        {
            var genericInterfaceType = typeof(IQsMessageHandler<>);
            var implmentationTypes = assembly.GetTypes()
                .Where(t => t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == genericInterfaceType) && t.IsClass && !t.IsAbstract);

            foreach (var implmentationType in implmentationTypes)
            {
                var interfaceType = implmentationType.GetInterfaces().First(i => i.IsGenericType && i.GetGenericTypeDefinition() == genericInterfaceType);
                services.AddTransient(interfaceType, implmentationType);
                var genericType = interfaceType.GetGenericArguments().First();
                _MessageHandlersStore.Add(new HandlersStoreRecord(interfaceType, implmentationType, genericType));
            }

            return services;
        }

    }
}
