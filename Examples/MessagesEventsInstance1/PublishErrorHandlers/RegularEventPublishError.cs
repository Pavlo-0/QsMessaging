using Contract.MessagesEventsInstance;
using QsMessaging.Public.Handler;

namespace MessagesEventsInstance1.PublishErrorHandlers
{
    internal class RegularEventPublishError : IQsMessagingPublishErrorHandler<RegularEventContract>
    {
        public Task HandlerErrorAsync(Exception ex, ErrorPublishDetail<RegularEventContract> errorModel)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return Task.CompletedTask;
        }
    }
}
