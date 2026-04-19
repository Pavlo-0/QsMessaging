using QsMessaging.RabbitMq.Models;
using QsMessaging.Shared.Models;

namespace QsMessaging.Shared.Services.Interfaces
{
    internal interface IHandlerService
    {
        IEnumerable<HandlersStoreRecord> GetHandlers(Type supportedInterfacesType);
        IEnumerable<HandlersStoreRecord> GetHandlers();

        IEnumerable<RqConsumerErrorHandlerStoreRecord> GetConsumerErrorHandlers();

        HandlersStoreRecord AddRRResponseHandler<TContract>();
    }
}
