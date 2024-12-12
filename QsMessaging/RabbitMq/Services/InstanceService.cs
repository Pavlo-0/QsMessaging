using Microsoft.Extensions.Logging;
using QsMessaging.RabbitMq.Services.Interfaces;

namespace QsMessaging.RabbitMq.Services
{
    internal class InstanceService(ILogger<InstanceService> logger) : IInstanceService
    {
        private static readonly Lazy<Guid> _instanceUID = new Lazy<Guid>(Guid.NewGuid);

        public Guid GetInstanceUID()
        {
            var uid = _instanceUID.Value;
            logger.LogInformation($"Instance ID: {uid}");
            return uid;
        }
    }
}
