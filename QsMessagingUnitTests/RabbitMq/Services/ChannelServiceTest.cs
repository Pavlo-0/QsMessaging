using Microsoft.Extensions.Logging;
using Moq;
using QsMessaging.RabbitMq.Models.Enums;
using QsMessaging.RabbitMq.Services;
using QsMessaging.RabbitMq.Services.Interfaces;
using QsMessaging.Shared.Interface;
using RabbitMQ.Client;
using System.Collections.Concurrent;
using System.Reflection;

namespace QsMessagingUnitTests.RabbitMq.Services
{
    [TestClass]
    public class ChannelServiceTest
    {
#pragma warning disable CS8618
        private Mock<ILogger<ChannelService>> _mockLogger;
        private Mock<IConnectionService> _mockConnectionService;
        private Mock<IConnection> _mockConnection;
        private Mock<IChannel> _mockChannel;
        private IChannelService _channelService;
#pragma warning restore CS8618

        [TestInitialize]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger<ChannelService>>();
            _mockConnectionService = new Mock<IConnectionService>();
            _mockConnection = new Mock<IConnection>();
            _mockChannel = new Mock<IChannel>();

            _channelService = new ChannelService(_mockLogger.Object, _mockConnectionService.Object);

            // Reset static dictionary between tests to ensure isolation
            var field = typeof(ChannelService).GetField("_channels", BindingFlags.NonPublic | BindingFlags.Static);
            field!.SetValue(null, new ConcurrentDictionary<ChannelPurpose, (IConnection, IChannel)>());

            
        }

        [TestMethod]
        public async Task GetOrCreateChannelAsync_WhenNoPreviousChannelExists_CreatesAndReturnsNewChannel()
        {
            _mockConnectionService
                .Setup(s => s.GetOrCreateConnectionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(_mockConnection.Object);
            _mockConnection
                .Setup(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(_mockChannel.Object);

            var result = await _channelService.GetOrCreateChannelAsync(_mockConnection.Object, ChannelPurpose.Common);

            Assert.IsNotNull(result);
            Assert.AreEqual(_mockChannel.Object, result);
            _mockConnectionService.Verify(s => s.GetOrCreateConnectionAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task GetOrCreateChannelAsync_WhenChannelAndConnectionAreOpen_ReturnsExistingChannelWithoutCreatingNew()
        {
            var existingChannel = new Mock<IChannel>();
            existingChannel.Setup(c => c.IsOpen).Returns(true);
            var existingConnection = new Mock<IConnection>();
            existingConnection.Setup(c => c.IsOpen).Returns(true);

            var field = typeof(ChannelService).GetField("_channels", BindingFlags.NonPublic | BindingFlags.Static);
            var dict = (ConcurrentDictionary<ChannelPurpose, (IConnection, IChannel)>)field!.GetValue(null)!;
            dict[ChannelPurpose.Common] = (existingConnection.Object, existingChannel.Object);

            var result = await _channelService.GetOrCreateChannelAsync(_mockConnection.Object, ChannelPurpose.Common);

            Assert.AreEqual(existingChannel.Object, result);
            _mockConnectionService.Verify(s => s.GetOrCreateConnectionAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [TestMethod]
        public async Task GetOrCreateChannelAsync_WhenExistingChannelIsClosed_CreatesAndReturnsNewChannel()
        {
            var closedChannel = new Mock<IChannel>();
            closedChannel.Setup(c => c.IsOpen).Returns(false);
            var openConnection = new Mock<IConnection>();
            openConnection.Setup(c => c.IsOpen).Returns(true);

            var field = typeof(ChannelService).GetField("_channels", BindingFlags.NonPublic | BindingFlags.Static);
            var dict = (ConcurrentDictionary<ChannelPurpose, (IConnection, IChannel)>)field!.GetValue(null)!;
            dict[ChannelPurpose.Common] = (openConnection.Object, closedChannel.Object);

            _mockConnectionService
                .Setup(s => s.GetOrCreateConnectionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(_mockConnection.Object);
            _mockConnection
                .Setup(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(_mockChannel.Object);

            var result = await _channelService.GetOrCreateChannelAsync(_mockConnection.Object, ChannelPurpose.Common);

            Assert.AreEqual(_mockChannel.Object, result);
            _mockConnectionService.Verify(s => s.GetOrCreateConnectionAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task GetOrCreateChannelAsync_WhenExistingConnectionIsClosed_CreatesAndReturnsNewChannel()
        {
            var openChannel = new Mock<IChannel>();
            openChannel.Setup(c => c.IsOpen).Returns(true);
            var closedConnection = new Mock<IConnection>();
            closedConnection.Setup(c => c.IsOpen).Returns(false);

            var field = typeof(ChannelService).GetField("_channels", BindingFlags.NonPublic | BindingFlags.Static);
            var dict = (ConcurrentDictionary<ChannelPurpose, (IConnection, IChannel)>)field!.GetValue(null)!;
            dict[ChannelPurpose.Common] = (closedConnection.Object, openChannel.Object);

            _mockConnectionService
                .Setup(s => s.GetOrCreateConnectionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(_mockConnection.Object);
            _mockConnection
                .Setup(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(_mockChannel.Object);

            var result = await _channelService.GetOrCreateChannelAsync(_mockConnection.Object, ChannelPurpose.Common);

            Assert.AreEqual(_mockChannel.Object, result);
            _mockConnectionService.Verify(s => s.GetOrCreateConnectionAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task GetOrCreateChannelAsync_WhenChannelCreationReturnsNull_ThrowsInvalidOperationException()
        {
            _mockConnectionService
                .Setup(s => s.GetOrCreateConnectionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(_mockConnection.Object);
            _mockConnection
                .Setup(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IChannel)null!);

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => _channelService.GetOrCreateChannelAsync(_mockConnection.Object, ChannelPurpose.Common));
        }

        [TestMethod]
        public async Task GetOrCreateChannelAsync_ForDifferentPurposes_CreatesDistinctChannels()
        {
            var secondChannel = new Mock<IChannel>();
            secondChannel.Setup(c => c.IsOpen).Returns(true);

            _mockConnectionService
                .Setup(s => s.GetOrCreateConnectionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(_mockConnection.Object);
            _mockConnection
                .SetupSequence(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(_mockChannel.Object)
                .ReturnsAsync(secondChannel.Object);

            var channel1 = await _channelService.GetOrCreateChannelAsync(_mockConnection.Object, ChannelPurpose.MessagePublish);
            var channel2 = await _channelService.GetOrCreateChannelAsync(_mockConnection.Object, ChannelPurpose.EventPublish);

            Assert.AreNotEqual(channel1, channel2);
        }

        [TestMethod]
        public void GetByConnection_WhenNoChannelsExist_ReturnsEmpty()
        {
            var result = _channelService.GetByConnection(_mockConnection.Object);

            Assert.IsFalse(result.Any());
        }

        [TestMethod]
        public void GetByConnection_WhenChannelsExistForConnection_ReturnsMatchingChannels()
        {
            var field = typeof(ChannelService).GetField("_channels", BindingFlags.NonPublic | BindingFlags.Static);
            var dict = (ConcurrentDictionary<ChannelPurpose, (IConnection, IChannel)>)field!.GetValue(null)!;
            dict[ChannelPurpose.Common] = (_mockConnection.Object, _mockChannel.Object);

            var result = _channelService.GetByConnection(_mockConnection.Object).ToList();

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(_mockChannel.Object, result[0]);
        }

        [TestMethod]
        public void GetByConnection_WhenChannelsExistForDifferentConnection_ReturnsEmpty()
        {
            var otherConnection = new Mock<IConnection>();

            var field = typeof(ChannelService).GetField("_channels", BindingFlags.NonPublic | BindingFlags.Static);
            var dict = (ConcurrentDictionary<ChannelPurpose, (IConnection, IChannel)>)field!.GetValue(null)!;
            dict[ChannelPurpose.Common] = (otherConnection.Object, _mockChannel.Object);

            var result = _channelService.GetByConnection(_mockConnection.Object);

            Assert.IsFalse(result.Any());
        }
    }
}
