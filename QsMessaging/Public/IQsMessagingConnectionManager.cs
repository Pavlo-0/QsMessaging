namespace QsMessaging.Public
{
    public interface IQsMessagingConnectionManager
    {
        /// <summary>
        /// Close the active messaging transport connection and release associated resources.
        /// </summary>
        /// <returns></returns>
        Task Close(CancellationToken cancellationToken = default);

        /// <summary>
        /// Open or re-establish the active messaging transport along with its associated consumers.
        /// </summary>
        /// <returns></returns>
        Task Open();

        /// <summary>
        /// Check if the active messaging transport connection is active.
        /// </summary>
        /// <returns></returns>
        bool IsConnected();
    }
}
