using Microsoft.Extensions.DependencyInjection;
using QsMessaging.Public.Handler;
using QsMessaging.RabbitMq.Models;
using QsMessaging.Shared.Interface;
using QsMessaging.Shared.Models;
using QsMessaging.Shared.Services.Interfaces;
using System.Collections.Concurrent;
using System.Reflection;

namespace QsMessaging.Shared.Services
{
    internal class HandlerService : IHandlerService
    {
        private static ConcurrentBag<HandlersStoreRecord> _handlers = new ConcurrentBag<HandlersStoreRecord>();
        private static ConcurrentBag<RqConsumerErrorHandlerStoreRecord> _consumerErrorHandler = new ConcurrentBag<RqConsumerErrorHandlerStoreRecord>();
        private static readonly object _handlersLock = new();
        private static readonly object _consumerErrorHandlersLock = new();

        private IServiceCollection _services;

        public HandlerService(IServiceCollection services, Assembly assembly)
            : this(services, new[] { assembly })
        {
        }

        public HandlerService(IServiceCollection services, IEnumerable<Assembly> assemblies)
        {
            _services = services;
            var assembliesToScan = assemblies
                .Where(assembly => assembly is not null)
                .Distinct()
                .ToArray();

            if (assembliesToScan.Length == 0)
            {
                throw new ArgumentException("At least one assembly must be provided for handler discovery.", nameof(assemblies));
            }

            foreach (var supportedInterfacesType in HardConfiguration.SupportedInterfacesTypes)
            {
                foreach (var findedHandler in assembliesToScan.SelectMany(assembly => FindHandlers(assembly, supportedInterfacesType)))
                {
                    DiscoveredConsumerHandlerCount++;
                    AddHandlerIfMissing(findedHandler);
                }
            }

            foreach (var assembly in assembliesToScan)
            {
                FindImplementations<IQsMessagingConsumerErrorHandler>(assembly);
            }
        }

        internal int DiscoveredConsumerHandlerCount { get; }

        public (HandlersStoreRecord record, bool isNew) AddRRResponseHandler<TContract>()
        {
            var record = new HandlersStoreRecord(
                typeof(IRRResponseHandler),
                typeof(IRRResponseHandler),
                typeof(RRResponseHandler),
                typeof(TContract));

            lock (_handlersLock)
            {
                if (!_handlers.Contains(record))
                {
                    _handlers.Add(record);
                    return (record, true);
                }
            }

            return (record, false);
        }

        public IEnumerable<HandlersStoreRecord> GetHandlers(Type supportedInterfacesType)
        {
            return _handlers.Where(r => r.supportedInterfacesType == supportedInterfacesType);
        }

        public IEnumerable<HandlersStoreRecord> GetHandlers()
        {
            return _handlers;
        }

        public IEnumerable<RqConsumerErrorHandlerStoreRecord> GetConsumerErrorHandlers()
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
                           .Select(type => new RqConsumerErrorHandlerStoreRecord(type));

            foreach (var record in records)
            {
                AddConsumerErrorHandlerIfMissing(record);
            }
        }

        private static void AddHandlerIfMissing(HandlersStoreRecord record)
        {
            lock (_handlersLock)
            {
                if (!_handlers.Contains(record))
                {
                    _handlers.Add(record);
                }
            }
        }

        private static void AddConsumerErrorHandlerIfMissing(RqConsumerErrorHandlerStoreRecord record)
        {
            lock (_consumerErrorHandlersLock)
            {
                if (!_consumerErrorHandler.Contains(record))
                {
                    _consumerErrorHandler.Add(record);
                }
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
