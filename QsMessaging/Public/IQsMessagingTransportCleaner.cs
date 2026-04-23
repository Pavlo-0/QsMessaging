namespace QsMessaging.Public
{
    /// <summary>
    /// Removes messaging entities created by the currently configured QsMessaging transport.
    /// Intended for local or debug cleanup scenarios before re-initializing the transport.
    /// </summary>
    public interface IQsMessagingTransportCleaner
    {
        Task CleanUp(CancellationToken cancellationToken = default);
    }
}
