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
        /// <param name="details">Additional details about the exception that occurred.</param>
        /// <returns></returns>
        Task HandleErrorAsync(Exception exception, ErrorConsumerDetail details);
    }

    /// <summary>
    /// A model designed to provide additional context and insights, helping to clarify the situation and identify the root cause of the problem.
    /// </summary>
    /// <param name="MessageObject">An object that encapsulates the message we received, providing context and details to help understand the situation and identify the underlying issue.</param>
    /// <param name="MessageBytes">An bytes that encapsulates the message we received, providing context and details to help understand the situation and identify the underlying issue.</param>
    /// <param name="QueueName"></param>
    /// <param name="SupportedInterfacesTypeName">Base handler interface</param>
    /// <param name="ConcreteHandlerInterfaceTypeName">Concreate base handler interface</param>
    /// <param name="HandlerTypeName">Implementation of concreate base handler interface</param>
    /// <param name="GenericTypeName">Model type name</param>
    /// <param name="ErrorType">The stage at which the error occurred.</param>
    public record ErrorConsumerDetail(
        object? MessageObject,
        byte[]? MessageBytes,
        string QueueName,
        string? SupportedInterfacesTypeName,
        string? ConcreteHandlerInterfaceTypeName,
        string? HandlerTypeName,
        string? GenericTypeName,
        ErrorConsumerType ErrorType);

    /// <summary>
    /// InHandlerProblem - Problem in your handler. Check your implementation
    /// RecevingProblem - Problem in receiving message. Check what you send.
    /// </summary>
    public enum ErrorConsumerType
    {
        InHandlerProblem,
        RecevingProblem,
    }

}
