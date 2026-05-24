using Microsoft.Extensions.Logging;
using QsMessaging.Shared.Services.Interfaces;

namespace QsMessaging.RabbitMq.Services
{
    internal class InstanceService(ILogger<InstanceService> logger) : IInstanceService
    {
        private static readonly Lazy<Guid> _instanceUID = new Lazy<Guid>(Guid.NewGuid);
        private static int _instanceUIDLogged;

        public Guid GetInstanceUID()
        {
            var uid = _instanceUID.Value;
            if (Interlocked.Exchange(ref _instanceUIDLogged, 1) == 0)
            {
                logger.LogInformation("Instance ID: {InstanceId}", uid);
            }

            return uid;
        }
    }
}
