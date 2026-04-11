namespace QsMessaging.RabbitMq.Models.Enums
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
