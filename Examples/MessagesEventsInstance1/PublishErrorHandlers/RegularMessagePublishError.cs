using Contract.MessagesEventsInstance;
using QsMessaging.Public.Handler;

namespace MessagesEventsInstance1.PublishErrorHandlers
{
    internal class RegularMessagePublishError : IQsMessagingPublishErrorHandler<RegularMessageContract>
    {
        public Task HandlerErrorAsync(Exception ex, ErrorPublishDetail<RegularMessageContract> errorModel)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return Task.CompletedTask;
        }
    }
}
