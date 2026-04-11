using QsMessaging.RabbitMq.Models;

namespace QsMessaging.Shared.Services.Interfaces
{
    internal interface IHandlerService
    {
        IEnumerable<HandlersStoreRecord> GetHandlers(Type supportedInterfacesType);
        IEnumerable<HandlersStoreRecord> GetHandlers();

        IEnumerable<ConsumerErrorHandlerStoreRecord> GetConsumerErrorHandlers();

        HandlersStoreRecord AddRRResponseHandler<TContract>();
    }
}
