using static QsMessaging.RabbitMq.Services.HandlerService;

namespace QsMessaging.RabbitMq.Services.Interfaces
{
    internal interface IHandlerService
    {
        IEnumerable<HandlersStoreRecord> GetHandlers(Type supportedInterfacesType);
        IEnumerable<HandlersStoreRecord> GetHandlers();
    }
}
