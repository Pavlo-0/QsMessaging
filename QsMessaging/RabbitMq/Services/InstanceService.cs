using QsMessaging.RabbitMq.Services.Interfaces;

namespace QsMessaging.RabbitMq.Services
{
    internal class InstanceService : IInstanceService
    {
        private static readonly Lazy<Guid> _instanceUID = new Lazy<Guid>(() => Guid.NewGuid());

        public Guid GetInstanceUID()
        {
            return _instanceUID.Value;
        }
    }
}
