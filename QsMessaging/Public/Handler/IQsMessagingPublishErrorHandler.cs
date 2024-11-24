
namespace QsMessaging.Public.Handler
{
    /// <summary>
    /// If an exception is generated during message publishing, the implementation will handle it according to the type contract and the generic type
    /// </summary>
    /// <typeparam name="TMessage">This generic type must match the published message type for the handler to be invoked</typeparam>
    public interface IQsMessagingPublishErrorHandler<TMessage> where TMessage : class
    {
        /// <summary>
        /// This method is called when an exception is thrown in the publish implementation.
        /// </summary>
        /// <param name="exception">The exception that was generated.</param>
        /// <param name="details">Additional details about the exception that occurred.</param>
        /// <returns></returns>
        Task HandlerErrorAsync(Exception ex , ErrorPublishDetail<TMessage> details);
    }

    /// <summary>
    /// A model designed to provide additional context and insights, helping to clarify the situation and identify the root cause of the problem.
    /// </summary>
    /// <typeparam name="TMessage">Contract (message) type</typeparam>
    /// <param name="Message">Contract (message)</param>
    /// <param name="ErrorType">The stage at which the error occurred.</param>
    /// <param name="MessageType">The type of publishing (as invoked by the method in the Sender) <see cref="IQsMessaging"/></param>
    public record ErrorPublishDetail<TMessage>(
        TMessage Message,
        ErrorPublishType ErrorType,
        string MessageType) where TMessage : class;

    /// <summary>
    /// PublishProblem - An exception occurred while publishing to RabbitMQ.
    /// EstablishPublishConnection - an exception occurred during the preparation for publishing
    /// </summary>
    public enum ErrorPublishType
    {
        PublishProblem,
        EstablishPublishConnection,
    }
}
