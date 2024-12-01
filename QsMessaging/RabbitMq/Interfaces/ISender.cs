namespace QsMessaging.RabbitMq.Interfaces
{
    internal interface ISender
    {
        Task SendMessageCorrelationAsync(object model, string? correlationId);
    }
}
