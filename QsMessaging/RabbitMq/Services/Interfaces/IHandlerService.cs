using QsMessaging.RabbitMq.Models;
using static QsMessaging.RabbitMq.Services.HandlerService;

namespace QsMessaging.RabbitMq.Services.Interfaces
{
    internal interface IHandlerService
    {
        IEnumerable<HandlersStoreRecord> GetHandlers(Type supportedInterfacesType);
        IEnumerable<HandlersStoreRecord> GetHandlers();

        IEnumerable<ConsumerErrorHandlerStoreRecord> GetConsumerErrorHandlers();

        HandlersStoreRecord AddRRResponseHandler<TContract>();
    }
}
