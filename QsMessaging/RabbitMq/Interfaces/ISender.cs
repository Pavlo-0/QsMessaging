namespace QsMessaging.RabbitMq.Interfaces
{
    internal interface ISender
    {
        Task SendMessageAsync(object model, Type type);
    }
}
