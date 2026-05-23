namespace QsMessaging.Public
{
    public class QsMessageHandlerRetryConfiguration : QsMessageReceiverRetryConfiguration
    {
        public QsMessageHandlerRetryConfiguration()
        {
            MaxRetryAttempts = 1;
        }
    }
}
