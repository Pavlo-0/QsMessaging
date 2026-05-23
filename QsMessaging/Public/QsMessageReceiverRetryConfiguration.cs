using Polly;

namespace QsMessaging.Public
{
    public class QsMessageReceiverRetryConfiguration
    {
        public int MaxRetryAttempts { get; set; } = 3;
        public TimeSpan Delay { get; set; } = TimeSpan.FromSeconds(1);
        public DelayBackoffType BackoffType { get; set; } = DelayBackoffType.Constant;
        public bool UseJitter { get; set; } = false;
    }
}
