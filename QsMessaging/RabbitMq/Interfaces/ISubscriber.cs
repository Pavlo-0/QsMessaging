namespace QsMessaging.RabbitMq.Interface
{
    internal interface ISubscriber
    {
        Task SubscribeMessageHandlerAsync(Type interfaceType, Type handlerType, Type genericHandlerType);

        Task SubscribeEventHandlerAsync(Type interfaceType, Type handlerType, Type genericHandlerType);
    }
}