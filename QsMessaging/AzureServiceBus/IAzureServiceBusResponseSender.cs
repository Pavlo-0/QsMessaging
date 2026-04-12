namespace QsMessaging.AzureServiceBus
{
    internal interface IAzureServiceBusResponseSender
    {
        Task SendResponseAsync(object model, string correlationId, string replyTo, CancellationToken cancellationToken = default);
    }
}
