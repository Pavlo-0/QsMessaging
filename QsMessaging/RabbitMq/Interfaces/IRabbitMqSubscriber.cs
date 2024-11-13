namespace QsMessaging.RabbitMq.Interface
{
    internal interface IRabbitMqSubscriber
    {
        Task SubscribeAsync(Type interfaceType, Type handlerType, Type genericHandlerType);
    }
}