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
        private static ConcurrentBag<HandlersStoreRecord> _publishErrorHandler = new ConcurrentBag<HandlersStoreRecord>();
        private static ConcurrentBag<ConsumerErrorHandlerStoreRecord> _consumerErrorHandler = new ConcurrentBag<ConsumerErrorHandlerStoreRecord>();

        public HandlerService(IServiceCollection services, Assembly assembly)
        {
            foreach (var supportedInterfacesType in HardConfiguration.SupportedInterfacesTypes)
            {
                foreach (var findedHandler in FindHandlers(assembly, supportedInterfacesType))
                {
                    _handlers.Add(findedHandler);
                } 
            }

            FindImplementations<IQsMessagingConsumerErrorHandler>(assembly);

            foreach (var findedHandler in FindHandlers(assembly, typeof(IQsMessagingPublishErrorHandler<>)))
            {
                _publishErrorHandler.Add(findedHandler);
            }
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
            return _consumerErrorHandler;
        }

        public IEnumerable<HandlersStoreRecord> GetPublishErrorHandlers()
        {
            return _publishErrorHandler;
        }

        public void RegisterAllHandlers(IServiceCollection services)
        {
            foreach (var handler in _handlers)
            {
                services.AddTransient(handler.ConcreteHandlerInterfaceType, handler.HandlerType);
            }

            foreach (var handler in _consumerErrorHandler)
            {
                services.AddTransient(typeof(IQsMessagingConsumerErrorHandler), handler.ConsumerErrorHandler);
            }

            foreach (var handler in _publishErrorHandler)
            {
                services.AddTransient(handler.ConcreteHandlerInterfaceType, handler.HandlerType);
            }
        }

        private IEnumerable<HandlersStoreRecord> FindHandlers(
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

            var returnHandlers = new List<HandlersStoreRecord>();

            foreach (var handlerType in handlersTypes)
            {
                var concreteHandlerInterfaceType = handlerType.GetInterfaces().First(i => i.IsGenericType && i.GetGenericTypeDefinition() == supportedInterfacesType);
                var genericType = concreteHandlerInterfaceType.GetGenericArguments().First();
                returnHandlers.Add(new HandlersStoreRecord(supportedInterfacesType, concreteHandlerInterfaceType, handlerType, genericType));
            }

            return returnHandlers;
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
                _consumerErrorHandler.Add(record);
            }
        }

        public record HandlersStoreRecord(Type supportedInterfacesType, Type ConcreteHandlerInterfaceType, Type HandlerType, Type GenericType);

        public record ConsumerErrorHandlerStoreRecord(Type ConsumerErrorHandler);

    }
}
