namespace QsMessaging.RabbitMq.Models.Enums
{
    public enum RqChannelPurpose
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
