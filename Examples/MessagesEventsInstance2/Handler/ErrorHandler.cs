using QsMessaging.Public.Handler;

namespace MessagesEventsInstance2.Handler
{
    internal class ErrorHandler : IQsMessagingConsumerErrorHandler
    {
        public Task HandleErrorAsync(Exception exception, ErrorConsumerDetail details)
        {
            ArgumentNullException.ThrowIfNull(exception);
            ArgumentNullException.ThrowIfNull(details);

            Console.WriteLine("QsMessage consumer error detected");
            Console.WriteLine($"Error type: {details.ErrorType}");
            Console.WriteLine($"Queue name: {details.QueueName}");
            Console.WriteLine($"Handler type: {details.HandlerTypeName}");
            Console.WriteLine($"Model type: {details.GenericTypeName}");
            Console.WriteLine($"Supported interface: {details.SupportedInterfacesTypeName}");
            Console.WriteLine($"Concrete interface: {details.ConcreteHandlerInterfaceTypeName}");
            Console.WriteLine($"Message object: {details.MessageObject}");
            Console.WriteLine($"Message bytes length: {details.MessageBytes?.Length ?? 0}");
            Console.WriteLine(exception);

            return Task.CompletedTask;
        }
    }
}
