namespace QsMessaging.RabbitMq.Services
{
    internal enum QueuePurpose
    {
        /// <summary>
        /// Single queue. Permanent.
        /// Message would be distributed to only one consumer which can consume message or wait when consumer would be ready.
        /// </summary>
        Permanent,

        /// <summary>
        /// Per Consumer queue. Temporary.
        /// Message would be distributed to all consumers.
        /// </summary>
        ConsumerTemporary,

        /// <summary>
        /// Per instance queue. Temporary.
        /// Every instance has own queue. Queue will be deleted after instance disconnect.
        /// Message would be distributed for one consumer in every instance.
        /// </summary>
        InstanceTemporary,

        /// <summary>
        /// Single queue. Temporary.
        /// Every instance consume one queue. Queue will be deleted after last consumer disconnect.
        /// Message would be distributed for one consumer.
        /// </summary>
        SingleTemporary
    }
}