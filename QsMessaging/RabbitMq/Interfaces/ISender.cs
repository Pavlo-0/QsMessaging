namespace QsMessaging.RabbitMq.Interfaces
{
    internal interface ISender
    {
        internal Task SendMessageCorrelationAsync(object model, string correlationId);
    }
}
