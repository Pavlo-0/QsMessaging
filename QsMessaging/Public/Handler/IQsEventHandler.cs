namespace QsMessaging.Public.Handler
{
    /// <summary>
    /// Implement this interface to receive events (messages). 
    /// The handler will only receive events when the application is active. 
    /// If the application is inactive (busy or not running), all events and messages will be discarded, and a notification will be sent.
    /// Note: There's no need to register it in the DI container, as QsMessaging takes care of it for you in services.AddTransient() way"
    /// </summary>
    /// <typeparam name="TModel"></typeparam>
    public interface IQsEventHandler<TModel> where TModel : class
    {
        /// <summary>
        /// This method is called when a message is received.
        /// This should be the exact same class as the one that was sent, including the namespace
        /// If the message cannot be received for any reason, it will disappear
        /// </summary>
        /// <param name="contract"></param>
        /// <returns></returns>
        Task Consumer(TModel contract);
    }
}