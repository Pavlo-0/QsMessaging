using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using QsMessaging.RabbitMq;
using QsMessaging.RabbitMq.Services;
using QsMessaging.RabbitMq.Services.Interfaces;
using QsMessaging.Services;
using QsMessaging.Services.Interfaces;

namespace QsMessaging.Public
{
    public static class QsMessagingRegistering
    {
        public static IServiceCollection AddQsMessaging(this IServiceCollection services, Action<QsMessagingConfiguration> options)
        {
            var configuration = new QsMessagingConfiguration();
            options(configuration);

            services.AddTransient<IQsMessaging, QsMessagingGate>();
            services.AddTransient<IRabbitMqSender, RabbitMqSender>();
            services.AddTransient<IExchangeNameGenerator, ExchangeNameGenerator>();

            services.AddSingleton<IRabbitMqConnectionStorage>(sp =>
            {
                return new RabbitMqConnectionStorage(configuration);
            });

            return services;
        }

        public static async Task<IHost> UseQsMessaging(this IHost host)
        {
            var connectionStorage = host.Services.GetRequiredService<IRabbitMqConnectionStorage>();
            var connection = connectionStorage.GetConnectionAsync();


            await connection;

            return host;
        }
    }
}
