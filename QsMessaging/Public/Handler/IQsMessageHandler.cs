namespace QsMessaging.Public.Handler
{
    /// Implement this interface to receive messages. 
    /// The handler will receive events as soon as they become available for consuming.
    /// If the application is inactive (busy or not running), all events and messages will WAIT.
    /// Note: There's no need to register it in the DI container, as QsMessaging takes care of it for you in services.AddTransient() way"
    public interface IQsMessageHandler<TModel> where TModel : class 
    {
        /// <summary>
        /// This method is called when a message is received.
        /// This should be the exact same class as the one that was sent, including the namespace
        /// If the message cannot be received for any reason, it will wait until receiving becomes possible and then be received.
        /// </summary>
        Task Consumer(TModel contractModel);
    }
}