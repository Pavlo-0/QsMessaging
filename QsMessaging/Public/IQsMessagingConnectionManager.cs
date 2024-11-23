namespace QsMessaging.Public
{
    public interface IQsMessagingConnectionManager
    {
        /// <summary>
        /// Close the connection to RabbitMQ.
        /// </summary>
        /// <returns></returns>
        Task Close();

        /// <summary>
        /// Re-establish the connection to RabbitMQ along with the associated queue and consumers.
        /// </summary>
        /// <returns></returns>
        Task Open();

        /// <summary>
        /// Check if the connection to RabbitMQ is active.
        /// </summary>
        /// <returns></returns>
        bool IsConnected();
    }
}
