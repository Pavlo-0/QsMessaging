using Microsoft.Extensions.DependencyInjection;
using QsMessaging.Public.Handler;
using QsMessaging.RabbitMq.Interfaces;
using QsMessaging.RabbitMq.Models;
using QsMessaging.RabbitMq.Services.Interfaces;
using System.Collections.Concurrent;
using System.Reflection;

namespace QsMessaging.RabbitMq.Services
{
    internal class HandlerService : IHandlerService
    {

        private static ConcurrentBag<HandlersStoreRecord> _handlers = new ConcurrentBag<HandlersStoreRecord>();
        private static ConcurrentBag<ConsumerErrorHandlerStoreRecord> _consumerErrorHandler = new ConcurrentBag<ConsumerErrorHandlerStoreRecord>();

        private IServiceCollection _services;

        public HandlerService(IServiceCollection services, Assembly assembly)
        {
            _services = services;
            foreach (var supportedInterfacesType in HardConfiguration.SupportedInterfacesTypes)
            {
                foreach (var findedHandler in FindHandlers(assembly, supportedInterfacesType))
                {
                    _handlers.Add(findedHandler);
                }
            }

            FindImplementations<IQsMessagingConsumerErrorHandler>(assembly);
        }

        public HandlersStoreRecord AddRRResponseHandler<TContract>()
        {
            var record = new HandlersStoreRecord(
                typeof(IRRResponseHandler),
                typeof(IRRResponseHandler),
                typeof(RRResponseHandler),
                typeof(TContract));

            _handlers.Add(record);

            return record;
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

        public void RegisterAllHandlers()
        {
            foreach (var handler in _handlers)
            {
                _services.AddTransient(handler.ConcreteHandlerInterfaceType, handler.HandlerType);
            }

            foreach (var handler in _consumerErrorHandler)
            {
                _services.AddTransient(typeof(IQsMessagingConsumerErrorHandler), handler.ConsumerErrorHandler);
            }

            _services.AddTransient(typeof(IRRResponseHandler), typeof(RRResponseHandler));
        }

        public static void FindImplementations<TInterface>(Assembly assembly)
        {

            var records = assembly.GetTypes()
                           .Where(type => typeof(TInterface).IsAssignableFrom(type)  // Check if type implements the interface
                                        && type.IsClass                     // Ensure it's a class
                                        && !type.IsAbstract)               // Ensure it's not abstract
                           .Select(type => new ConsumerErrorHandlerStoreRecord(type));
            //TODO: Refactor. Remove specefic type ConsumerErrorHandlerStoreRecord from method.

            foreach (var record in records)
            {
                _consumerErrorHandler.Add(record);
            }
        }

        private IEnumerable<HandlersStoreRecord> FindHandlers(Assembly assembly, Type supportedInterfacesType)
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
    }
}
