using QsMessaging.Shared.Services;

namespace QsMessaging.AzureServiceBus
{
    internal static class AsbMessageHandlerExecutionContext
    {
        public static bool IsInsideHandler => MessageHandlerExecutionContext.IsInsideHandler;

        public static MessageHandlerExecutionContext.Scope Enter()
        {
            return MessageHandlerExecutionContext.Enter();
        }
    }
}
