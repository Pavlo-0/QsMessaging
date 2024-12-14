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
        /// <typeparam name="Model">The type of the message to send. Must be a class or record, but cannot be <see cref="string"/> or <see cref="object"/>.</typeparam>
        /// <exception cref="NotSupportedException">Thrown if <typeparamref name="TMessage"/> is <see cref="string"/> or <see cref="object"/>.</exception>
        Task SendMessageAsync<TMessage>(TMessage Model) where TMessage : class;

        /// <summary>
        /// Send a message to another service. 
        /// On the receiving side, you must implement a handler that consumes the exact same message (by type). 
        /// If the message cannot be delivered immediately, it will be discarded.
        /// <see cref="IQsEventHandler"/>
        /// </summary>
        /// <typeparam name="TEvent">The type of the message. This type will be used to determine which handler should be called to consume the message.</typeparam>
        /// <typeparam name="Model">The type of the message to send. Must be a class or record, but cannot be <see cref="string"/> or <see cref="object"/>.</typeparam>
        /// <exception cref="NotSupportedException">Thrown if <typeparamref name="TEvent"/> is <see cref="string"/> or <see cref="object"/>.</exception>
        Task SendEventAsync<TEvent>(TEvent Model) where TEvent : class;

        /// <summary>
        /// Send a request to another service and wait for the response
        /// On the receiving side, you must implement a handler that consumes the exact same Request (by type) and return the exact same Response.
        /// If a request cannot be delivered immediately, it will wait until a consumer is ready to process it. 
        /// If all possible consumers are disconnected, the request will be discarded.
        /// <see cref="IQsRequestResponseHandler<TRequest, TResponse>"/>
        /// </summary>
        /// <typeparam name="TRequest">The type of the request. This type will be used to determine which handler should be called to consume the request.</typeparam>
        /// <typeparam name="TResponse">The type of the response. This type will be used to determine which handler should be called to consume the request. 
        /// Must be a class or record, but cannot be <see cref="string"/> or <see cref="object"/>.
        /// The request type and response type must be different and cannot be the same. </typeparam>
        /// <typeparam name="request">The type of the message to request. Must be a class or record, but cannot be <see cref="string"/> or <see cref="object"/>.</typeparam>
        /// <exception cref="NotSupportedException">Thrown if <typeparamref name="TRequest"/> is <see cref="string"/> or <see cref="object"/>.</exception>
        /// <exception cref="NotSupportedException">Thrown if <typeparamref name="TResponse"/> is <see cref="string"/> or <see cref="object"/>.</exception>
        /// <exception cref="NotSupportedException">Thrown if <typeparamref name="TRequest"/> and <typeparamref name="TResponse"/> the same type.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="request"/> is null.</exception>
        /// <returns></returns>
        Task<TResponse> RequestResponse<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default) where TRequest : class where TResponse : class;
    }
}
