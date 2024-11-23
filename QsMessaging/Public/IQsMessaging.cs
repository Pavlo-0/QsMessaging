namespace QsMessaging.Public
{
    public interface IQsMessaging
    {
        /// <summary>
        /// Send a message to another service. 
        /// On the receiving side, you must implement a handler that consumes the exact same message (by type). 
        /// If the message cannot be delivered immediately, it will wait until it can be successfully received.
        /// <see cref="IQsMessageHandler"/>
        /// If the handler receiver is an <see cref="IQsEventHandler"/>, the message can only be guaranteed to be delivered if the receiving application is active.
        /// </summary>
        /// <typeparam name="TMessage">The type of the message. This type will be used to determine which handler should be called to consume the message.</typeparam>
        /// <param name="Model"></param>
        /// <returns></returns>
        Task<bool> SendMessageAsync<TMessage>(TMessage Model);

        /// <summary>
        /// Send a message to another service. 
        /// On the receiving side, you must implement a handler that consumes the exact same message (by type). 
        /// If the message cannot be delivered immediately, it will be discarded.
        /// <see cref="IQsEventHandler"/>
        /// </summary>
        /// <typeparam name="TEvent">The type of the message. This type will be used to determine which handler should be called to consume the message.</typeparam>
        /// <param name="Model"></param>
        /// <returns></returns>
        Task<bool> SendEventAsync<TEvent>(TEvent Model);
    }
}
