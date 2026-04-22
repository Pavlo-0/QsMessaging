using Microsoft.Extensions.Logging;
using Moq;
using QsMessaging.RabbitMq.Models.Enums;
using QsMessaging.RabbitMq.Services;
using QsMessaging.RabbitMq.Services.Interfaces;
using RabbitMQ.Client;
using System.Collections.Concurrent;
using System.Reflection;

namespace QsMessagingUnitTests.RabbitMq.Services
{
    [TestClass]
    public class ChannelServiceTest
    {
#pragma warning disable CS8618
        private Mock<ILogger<RqChannelService>> _mockLogger;
        private Mock<IRqConnectionService> _mockConnectionService;
        private Mock<IConnection> _mockConnection;
        private Mock<IChannel> _mockChannel;
        private RqChannelService _channelService;
#pragma warning restore CS8618

        [TestInitialize]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger<RqChannelService>>();
            _mockConnectionService = new Mock<IRqConnectionService>();
            _mockConnection = new Mock<IConnection>();
            _mockChannel = new Mock<IChannel>();
            _mockConnection.SetupGet(c => c.IsOpen).Returns(true);
            _mockChannel.SetupGet(c => c.IsOpen).Returns(true);

            _channelService = new RqChannelService(_mockLogger.Object, _mockConnectionService.Object);
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

            var result = await _channelService.GetOrCreateChannelAsync(RqChannelPurpose.Common);

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

            GetChannels()[RqChannelPurpose.Common] = (existingConnection.Object, existingChannel.Object);

            var result = await _channelService.GetOrCreateChannelAsync(RqChannelPurpose.Common);

            Assert.AreEqual(existingChannel.Object, result);
            _mockConnectionService.Verify(s => s.GetOrCreateConnectionAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [TestMethod]
        public async Task GetOrCreateChannelAsync_WhenExistingChannelIsClosed_CreatesAndReturnsNewChannel()
        {
            var closedChannel = new Mock<IChannel>();
            closedChannel.Setup(c => c.IsOpen).Returns(false);
            closedChannel.As<IAsyncDisposable>().Setup(c => c.DisposeAsync()).Returns(ValueTask.CompletedTask);
            var openConnection = new Mock<IConnection>();
            openConnection.Setup(c => c.IsOpen).Returns(true);

            GetChannels()[RqChannelPurpose.Common] = (openConnection.Object, closedChannel.Object);

            _mockConnectionService
                .Setup(s => s.GetOrCreateConnectionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(_mockConnection.Object);
            _mockConnection
                .Setup(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(_mockChannel.Object);

            var result = await _channelService.GetOrCreateChannelAsync(RqChannelPurpose.Common);

            Assert.AreEqual(_mockChannel.Object, result);
            _mockConnectionService.Verify(s => s.GetOrCreateConnectionAsync(It.IsAny<CancellationToken>()), Times.Once);
            closedChannel.As<IAsyncDisposable>().Verify(c => c.DisposeAsync(), Times.Once);
        }

        [TestMethod]
        public async Task GetOrCreateChannelAsync_WhenExistingConnectionIsClosed_CreatesAndReturnsNewChannel()
        {
            var openChannel = new Mock<IChannel>();
            openChannel.Setup(c => c.IsOpen).Returns(true);
            openChannel.As<IAsyncDisposable>().Setup(c => c.DisposeAsync()).Returns(ValueTask.CompletedTask);
            var closedConnection = new Mock<IConnection>();
            closedConnection.Setup(c => c.IsOpen).Returns(false);

            GetChannels()[RqChannelPurpose.Common] = (closedConnection.Object, openChannel.Object);

            _mockConnectionService
                .Setup(s => s.GetOrCreateConnectionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(_mockConnection.Object);
            _mockConnection
                .Setup(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(_mockChannel.Object);

            var result = await _channelService.GetOrCreateChannelAsync(RqChannelPurpose.Common);

            Assert.AreEqual(_mockChannel.Object, result);
            _mockConnectionService.Verify(s => s.GetOrCreateConnectionAsync(It.IsAny<CancellationToken>()), Times.Once);
            openChannel.As<IAsyncDisposable>().Verify(c => c.DisposeAsync(), Times.Once);
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
                () => _channelService.GetOrCreateChannelAsync(RqChannelPurpose.Common));
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

            var channel1 = await _channelService.GetOrCreateChannelAsync(RqChannelPurpose.MessagePublish);
            var channel2 = await _channelService.GetOrCreateChannelAsync(RqChannelPurpose.EventPublish);

            Assert.AreNotEqual(channel1, channel2);
        }

        [TestMethod]
        public async Task CloseByConnectionAsync_WhenNoChannelsMatch_LeavesOtherChannelsUntouched()
        {
            var otherConnection = new Mock<IConnection>();
            var otherChannel = new Mock<IChannel>();
            otherChannel.Setup(c => c.IsOpen).Returns(true);

            var channels = GetChannels();
            channels[RqChannelPurpose.Common] = (otherConnection.Object, otherChannel.Object);

            await _channelService.CloseByConnectionAsync(_mockConnection.Object);

            Assert.AreEqual(1, channels.Count);
            Assert.AreSame(otherChannel.Object, channels[RqChannelPurpose.Common].channel);
        }

        [TestMethod]
        public async Task CloseByConnectionAsync_RemovesAndDisposesMatchingChannels()
        {
            _mockChannel.Setup(c => c.IsOpen).Returns(false);
            _mockChannel
                .As<IAsyncDisposable>()
                .Setup(c => c.DisposeAsync())
                .Returns(ValueTask.CompletedTask);

            var otherConnection = new Mock<IConnection>();
            var otherChannel = new Mock<IChannel>();
            otherChannel.Setup(c => c.IsOpen).Returns(true);

            var channels = GetChannels();
            channels[RqChannelPurpose.Common] = (_mockConnection.Object, _mockChannel.Object);
            channels[RqChannelPurpose.EventPublish] = (otherConnection.Object, otherChannel.Object);

            await _channelService.CloseByConnectionAsync(_mockConnection.Object);

            Assert.IsFalse(channels.ContainsKey(RqChannelPurpose.Common));
            Assert.IsTrue(channels.ContainsKey(RqChannelPurpose.EventPublish));
            Assert.AreSame(otherChannel.Object, channels[RqChannelPurpose.EventPublish].channel);
            _mockChannel.As<IAsyncDisposable>().Verify(c => c.DisposeAsync(), Times.Once);
        }

        private ConcurrentDictionary<RqChannelPurpose, (IConnection connection, IChannel channel)> GetChannels()
        {
            var field = typeof(RqChannelService).GetField("_channels", BindingFlags.NonPublic | BindingFlags.Instance);
            return (ConcurrentDictionary<RqChannelPurpose, (IConnection connection, IChannel channel)>)field!.GetValue(_channelService)!;
        }
    }
}
