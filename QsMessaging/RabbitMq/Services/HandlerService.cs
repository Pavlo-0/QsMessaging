using Microsoft.Extensions.DependencyInjection;
using QsMessaging.Public.Handler;
using QsMessaging.RabbitMq.Services.Interfaces;
using System.Collections.Concurrent;
using System.Reflection;

namespace QsMessaging.RabbitMq.Services
{
    internal class HandlerService : IHandlerService
    {

        private static ConcurrentBag<HandlersStoreRecord> _handlers = new ConcurrentBag<HandlersStoreRecord>();
        private static ConcurrentBag<ConsumerErrorHandlerStoreRecord> _consumerErrorHandlerHandlers = new ConcurrentBag<ConsumerErrorHandlerStoreRecord>();

        public HandlerService(IServiceCollection services, Assembly assembly)
        {
            foreach (var supportedInterfacesType in HardConfiguration.SupportedInterfacesTypes)
            {
                FindHandlers(services, assembly, supportedInterfacesType);
            }

            FindImplementations<IQsMessagingConsumerErrorHandler>(assembly);
        }

        public IEnumerable<HandlersStoreRecord> GetHandlers(Type supportedInterfacesType)
        {
            return _handlers.Where(r => r.supportedInterfacesType == supportedInterfacesType);
        }

        public IEnumerable<HandlersStoreRecord> GetHandlers()
        {
            return _handlers;
        }

        public IEnumerable<ConsumerErrorHandlerStoreRecord> GetConsumerErrorHandlers()
        {
            return _consumerErrorHandlerHandlers;
        }

        public void RegisterAllHandlers(IServiceCollection services)
        {
            foreach (var handler in _handlers)
            {
                services.AddTransient(handler.ConcreteHandlerInterfaceType, handler.HandlerType);
            }

            foreach (var handler in _consumerErrorHandlerHandlers)
            {
                services.AddTransient(typeof(IQsMessagingConsumerErrorHandler), handler.ConsumerErrorHandler);
            }
        }

        private void FindHandlers(
           IServiceCollection services,
           Assembly assembly,
           Type supportedInterfacesType)
        {
            var handlersTypes = assembly.GetTypes()
                .Where(
                    t => t.GetInterfaces()
                    .Any(i => i.IsGenericType &&
                         i.GetGenericTypeDefinition() == supportedInterfacesType) &&
                    t.IsClass &&
                    !t.IsAbstract);

            foreach (var handlerType in handlersTypes)
            {
                var concreteHandlerInterfaceType = handlerType.GetInterfaces().First(i => i.IsGenericType && i.GetGenericTypeDefinition() == supportedInterfacesType);
                var genericType = concreteHandlerInterfaceType.GetGenericArguments().First();
                _handlers.Add(new HandlersStoreRecord(supportedInterfacesType, concreteHandlerInterfaceType, handlerType, genericType));

            }
        }

        public static void FindImplementations<TInterface>(Assembly assembly)
        {
            
            var records = assembly.GetTypes()
                           .Where(type => typeof(TInterface).IsAssignableFrom(type)  // Check if type implements the interface
                                        && type.IsClass                     // Ensure it's a class
                                        && !type.IsAbstract)               // Ensure it's not abstract
                           .Select(type => new ConsumerErrorHandlerStoreRecord(type));

            foreach (var record in records)
            {
                _consumerErrorHandlerHandlers.Add(record);
            }
        }

        public record HandlersStoreRecord(Type supportedInterfacesType, Type ConcreteHandlerInterfaceType, Type HandlerType, Type GenericType);

        public record ConsumerErrorHandlerStoreRecord(Type ConsumerErrorHandler);

    }
}
