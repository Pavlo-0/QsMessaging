using Microsoft.Extensions.Logging;
using Moq;
using QsMessaging.RabbitMq.Services;

namespace QsMessagingUnitTests.RabbitMq.Services
{
    [TestClass]
    public class InstanceServiceTest
    {
#pragma warning disable CS8618
        private Mock<ILogger<InstanceService>> _mockLogger;
#pragma warning restore CS8618

        [TestInitialize]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger<InstanceService>>();
        }

        [TestMethod]
        public void GetInstanceUID_ReturnsNonEmptyGuid()
        {
            var service = new InstanceService(_mockLogger.Object);

            var result = service.GetInstanceUID();

            Assert.AreNotEqual(Guid.Empty, result);
        }

        [TestMethod]
        public void GetInstanceUID_CalledTwice_ReturnsSameGuid()
        {
            var service = new InstanceService(_mockLogger.Object);

            var first = service.GetInstanceUID();
            var second = service.GetInstanceUID();

            Assert.AreEqual(first, second);
        }

        [TestMethod]
        public void GetInstanceUID_AcrossDifferentServiceInstances_ReturnsSameGuid()
        {
            var service1 = new InstanceService(_mockLogger.Object);
            var service2 = new InstanceService(_mockLogger.Object);

            var guid1 = service1.GetInstanceUID();
            var guid2 = service2.GetInstanceUID();

            Assert.AreEqual(guid1, guid2);
        }
    }
}
