using Microsoft.Extensions.DependencyInjection;
using QsMessaging.Services;
using QsMessaging.Services.Interfaces;

namespace QsMessaging.Public
{
    public static class QsMessagingRegistering
    {
        public static IServiceCollection AddQsMessaging(this IServiceCollection services)
        {
            services.AddTransient<IQsMessaging, QsMessagingGate>();
            services.AddTransient<IRabbitMqSender, RabbitMqSender>();
            
            return services;
        }
    }
}
