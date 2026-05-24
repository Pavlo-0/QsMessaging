namespace QsMessaging.Public
{
    /// <summary>
    /// Removes QsMessaging-prefixed entities visible to the currently configured transport scope.
    /// Set AllowDangerousFullCleanup to true to remove every visible entity in that scope.
    /// Intended for debug or local reset scenarios.
    /// </summary>
    public interface IQsMessagingTransportFullCleaner
    {
        Task FullCleanUp(CancellationToken cancellationToken = default);
    }
}
