namespace QsMessaging.Public.Handler
{
    /// <summary>
    /// Defines a contract for handling incoming messages and returning a response.
    /// Only one handler instance processes events as they become available.
    /// If the application is inactive (not running), events and messages will not be received. 
    /// However, if the application is running but the handler is busy, it will process messages as soon as possible.
    /// Note: This interface is automatically registered in the dependency injection (DI) container by QsMessaging with a transient lifecycle.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request. This type will be used to determine which handler should be called to consume the request.</typeparam>
    /// <typeparam name="TResponse">The type of the response. This type will be used to determine which handler should be called to consume the request. 
    /// Must be a class or record, but cannot be <see cref="string"/> or <see cref="object"/>.
    /// The request type and response type must be different and cannot be the same. </typeparam>
    /// <typeparam name="request">The type of the message to request. Must be a class or record, but cannot be <see cref="string"/> or <see cref="object"/>.</typeparam>

    public interface IQsRequestResponseHandler<TRequest, TResponse>
    {
        Task<TResponse> Consumer(TRequest request);
    }
}
