using Microsoft.Extensions.DependencyInjection;
using QsMessaging.RabbitMq.Services.Interfaces;
using System.Collections.Concurrent;
using System.Reflection;

namespace QsMessaging.RabbitMq.Services
{
    internal class HandlerService : IHandlerService
    {

        private static ConcurrentBag<HandlersStoreRecord> _handlers
           = new ConcurrentBag<HandlersStoreRecord>();

        public HandlerService(IServiceCollection services, Assembly assembly)
        {
            foreach (var supportedInterfacesType in HardConfiguration.SupportedInterfacesTypes)
            {
                FindHandlers(services, assembly, supportedInterfacesType);
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
                services.AddTransient(concreteHandlerInterfaceType, handlerType);

                _handlers.Add(new HandlersStoreRecord(supportedInterfacesType, concreteHandlerInterfaceType, handlerType, genericType));

            }
        }

        public record HandlersStoreRecord(Type supportedInterfacesType, Type ConcreteHandlerInterfaceType, Type HandlerType, Type GenericType);
    }
}
