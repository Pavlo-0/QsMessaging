namespace QsMessaging.RabbitMq.Services
{
    public enum ChannelPurpose
    {
        Common,
        MessagePublish,
        EventPublish,
        QueuePermanent,
        QueueConsumerTemporary,
        QueueInstanceTemporary,
        QueueSingleTemporary,
    }
}
