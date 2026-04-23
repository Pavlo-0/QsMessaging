namespace QsMessaging.Public
{
    /// <summary>
    /// Removes all messaging entities visible to the currently configured QsMessaging transport scope.
    /// For RabbitMQ this means the configured virtual host, and for Azure Service Bus this means the configured namespace.
    /// Intended for debug or local reset scenarios.
    /// </summary>
    public interface IQsMessagingTransportFullCleaner
    {
        Task FullCleanUp(CancellationToken cancellationToken = default);
    }
}
