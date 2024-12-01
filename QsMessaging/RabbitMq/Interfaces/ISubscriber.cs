namespace QsMessaging.RabbitMq.Interface
{
    internal interface ISubscriber
    {
        Task Subscribe();
        //Task SubscribeHandlerAsync();
    }
}