namespace QsMessaging.RabbitMq.Models
{
    internal record HandlersStoreRecord(Type supportedInterfacesType, Type ConcreteHandlerInterfaceType, Type HandlerType, Type GenericType);

}
