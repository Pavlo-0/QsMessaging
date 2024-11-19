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
        private record HandlersStoreRecord(Type ConcreteHandlerInterfaceType, Type HandlerType, Type GenericType);

        private enum HandlerType
        {
            Message,
            Event
        }

        private static readonly ConcurrentBag<HandlersStoreRecord> _MessageHandlersStore = new ConcurrentBag<HandlersStoreRecord>();
        private static readonly ConcurrentBag<HandlersStoreRecord> _EventHandlersStore = new ConcurrentBag<HandlersStoreRecord>();

        public static IServiceCollection AddQsMessaging(this IServiceCollection services, Action<QsMessagingConfiguration> options)
        {
            var configuration = new QsMessagingConfiguration();
            options(configuration);

            services.AddTransient<IQsMessaging, QsMessagingGate>();
            services.AddTransient<IRabbitMqSender, Sender>();
            services.AddTransient<INameGenerator, NameGenerator>();
            services.AddTransient<IExchangeGenerator, ExchangeGenerator>();
            services.AddTransient<IQueueGenerator, QueueGenerator>();
            services.AddTransient<IQsMessagingConnectionManager, ConnectionManager>();

            services.AddSingleton<IConnectionWorker>(sp =>
            {
                return new ConnectionWorker(configuration);
            });

            //Handler part
            services.AddTransient<ISubscriber, Subscriber>();

            var assembly = Assembly.GetEntryAssembly();

            if (assembly is null)
                throw new ArgumentNullException("Entry Assembly is null. Auto register fail.");

            RegisterMessageHandlers(services, assembly);
            RegisterEventHandlers(services, assembly);
            return services;
        }

        public static async Task<IHost> UseQsMessaging(this IHost host)
        {
            var connectionStorage = host.Services.GetRequiredService<IConnectionWorker>();
            var connection = connectionStorage.GetOrCreateConnectionAsync();

            var subscriber = host.Services.GetRequiredService<ISubscriber>();
            foreach (var record in _MessageHandlersStore)
            {
                await subscriber.SubscribeMessageHandlerAsync(record.ConcreteHandlerInterfaceType, record.HandlerType, record.GenericType);
            }

            foreach (var record in _EventHandlersStore)
            {
                await subscriber.SubscribeEventHandlerAsync(record.ConcreteHandlerInterfaceType, record.HandlerType, record.GenericType);
            }

            await connection;

            return host;
        }

        private static IServiceCollection RegisterMessageHandlers(this IServiceCollection services, Assembly assembly)
        {
            GetHandlers(services, assembly, typeof(IQsMessageHandler<>), _MessageHandlersStore);
            return services;
        }

        private static IServiceCollection RegisterEventHandlers(this IServiceCollection services, Assembly assembly)
        {
            GetHandlers(services, assembly, typeof(IQsEventHandler<>), _EventHandlersStore);
            return services;
        }


        private static IServiceCollection GetHandlers(
            IServiceCollection services, 
            Assembly assembly, 
            Type handlerInterfaceType, 
            ConcurrentBag<HandlersStoreRecord> eventHandlersStore)
        {
            var handlersTypes = assembly.GetTypes()
                .Where(
                    t => t.GetInterfaces()
                    .Any(i => i.IsGenericType &&
                         i.GetGenericTypeDefinition() == handlerInterfaceType) &&
                    t.IsClass &&
                    !t.IsAbstract);

            foreach (var handlerType in handlersTypes)
            {
                var concreteHandlerInterfaceType = handlerType.GetInterfaces().First(i => i.IsGenericType && i.GetGenericTypeDefinition() == handlerInterfaceType);
                var genericType = concreteHandlerInterfaceType.GetGenericArguments().First();
                services.AddTransient(concreteHandlerInterfaceType, handlerType);
                eventHandlersStore.Add(new HandlersStoreRecord(concreteHandlerInterfaceType, handlerType, genericType));
            }

            return services;
        }

    }
}
