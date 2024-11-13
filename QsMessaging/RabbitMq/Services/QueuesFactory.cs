using QsMessaging.RabbitMq.Services.Interfaces;
using RabbitMQ.Client;

namespace QsMessaging.RabbitMq.Services
{
    internal class QueuesGenerator : IQueuesGenerator
    {
        public async Task CreateQueues(IChannel channel, string exchangeName)
        {
            try
            {
                await channel.ExchangeDeclareAsync(
                               exchange: exchangeName,
                               type: ExchangeType.Fanout,
                               durable: true,
                               autoDelete: false,
                               arguments: null);
            }
            catch (Exception ex)
            {
                // Log the exception
            }
        }
    }
}
