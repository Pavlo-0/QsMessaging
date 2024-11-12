
namespace QsMessaging.RabbitMq
{
    internal interface IRabbitMqSubscriber
    {
        Task SubscribeAsync(Type interfaceType, Type handlerType, Type genericHandlerType);
    }
}