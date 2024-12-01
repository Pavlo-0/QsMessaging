namespace QsMessaging.RabbitMq.Interfaces
{
    internal interface IRRResponseHandler
    {
        Task Consumer(object contract, string correlationId);
    }
}
