namespace QsMessaging.Public.Handler
{
    /// <summary>
    /// If any consumer implementation generates an exception, that implementation will handle the exception automatically. 
    /// There's no need to register it in the DI container, as QsMessaging takes care of it for you.
    /// </summary>
    public interface IQsMessagingConsumerErrorHandler
    {
        /// <summary>
        /// This method is called when an exception is thrown in the consumer implementation.
        /// </summary>
        /// <param name="exception">The exception that was generated.</param>
        /// <param name="message">Additional details about the events that occurred.</param>
        /// <returns></returns>
        Task HandleErrorAsync(Exception exception, object? message);
    }
}
