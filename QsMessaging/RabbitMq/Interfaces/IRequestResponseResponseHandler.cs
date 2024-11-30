namespace QsMessaging.RabbitMq.Interfaces
{
    internal interface IRequestResponseResponseHandler
    {
        Task Consumer(object contract, string correlationId);
    }
}
