using QsMessaging.RabbitMq.Interface;
using QsMessaging.RabbitMq.Services.Interfaces;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace QsMessaging.RabbitMq.Services
{
    internal class ExchangeGenerator(INameGenerator exchangeNameGenerator) : IExchangeGenerator
    {
        public async Task<string> CreateExchange(IChannel channel, Type TModel)
        {
            var name = exchangeNameGenerator.GetExchangeNameFromType(TModel);

            await channel.ExchangeDeclareAsync(
                           exchange: name,
                           type: ExchangeType.Fanout,
                           durable: true,
                           autoDelete: false,
                           arguments: null);

            /*
            try
            {
                await channel.ExchangeDeclareAsync(
                               exchange: name,
                               type: ExchangeType.Fanout,
                               durable: true,
                               autoDelete: false,
                               arguments: null);
            }
            catch (OperationInterruptedException ex)
            {
                //
            }*/

            return name;
        }
    }
}
