using Microsoft.Extensions.Logging;
using Moq;
using QsMessaging.Public;
using QsMessaging.RabbitMq.Interfaces;
using QsMessaging.RabbitMq.Models;
using System.Collections.Concurrent;
using System.Reflection;

namespace QsMessagingUnitTests.RabbitMq
{
    [TestClass]
    public class RequestResponseMessageStoreTest
    {
#pragma warning disable CS8618
        private Mock<ILogger<RequestResponseMessageStore>> _mockLogger;
#pragma warning restore CS8618

        private class TestResponse { public string Name { get; set; } = string.Empty; }

        [TestInitialize]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger<RequestResponseMessageStore>>();

            // Reset static dictionary between tests
            var field = typeof(RequestResponseMessageStore).GetField("storeConsumerRecords", BindingFlags.NonPublic | BindingFlags.Static);
            field!.SetValue(null, new ConcurrentDictionary<string, StoreMessageRecord>());
        }

        [TestMethod]
        public void AddRequestMessageAsync_ReturnsIncompleteTask()
        {
            var mockConfig = new Mock<IQsMessagingConfiguration>();
            mockConfig.Setup(c => c.RequestResponseTimeout).Returns(60_000);
            var store = new RequestResponseMessageStore(_mockLogger.Object, mockConfig.Object);

            var task = store.AddRequestMessageAsync("id-1", new object(), CancellationToken.None);

            Assert.IsFalse(task.IsCompleted);
        }

        [TestMethod]
        public async Task MarkAsResponded_WhenCorrelationIdExists_CompletesTask()
        {
            var mockConfig = new Mock<IQsMessagingConfiguration>();
            mockConfig.Setup(c => c.RequestResponseTimeout).Returns(60_000);
            var store = new RequestResponseMessageStore(_mockLogger.Object, mockConfig.Object);
            var response = new TestResponse { Name = "resp" };

            var task = store.AddRequestMessageAsync("id-1", new object(), CancellationToken.None);
            store.MarkAsResponded("id-1", response);
            await task;

            Assert.IsTrue(task.IsCompleted);
        }

        [TestMethod]
        public void MarkAsResponded_WhenCorrelationIdDoesNotExist_DoesNotThrow()
        {
            var mockConfig = new Mock<IQsMessagingConfiguration>();
            mockConfig.Setup(c => c.RequestResponseTimeout).Returns(60_000);
            var store = new RequestResponseMessageStore(_mockLogger.Object, mockConfig.Object);

            store.MarkAsResponded("unknown-id", new object());
        }

        [TestMethod]
        public void IsRespondedMessage_WhenNotYetResponded_ReturnsFalse()
        {
            var mockConfig = new Mock<IQsMessagingConfiguration>();
            mockConfig.Setup(c => c.RequestResponseTimeout).Returns(60_000);
            var store = new RequestResponseMessageStore(_mockLogger.Object, mockConfig.Object);

            _ = store.AddRequestMessageAsync("id-1", new object(), CancellationToken.None);

            var result = store.IsRespondedMessage("id-1");

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void IsRespondedMessage_WhenResponded_ReturnsTrue()
        {
            var mockConfig = new Mock<IQsMessagingConfiguration>();
            mockConfig.Setup(c => c.RequestResponseTimeout).Returns(60_000);
            var store = new RequestResponseMessageStore(_mockLogger.Object, mockConfig.Object);

            _ = store.AddRequestMessageAsync("id-1", new object(), CancellationToken.None);
            store.MarkAsResponded("id-1", new object());

            var result = store.IsRespondedMessage("id-1");

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void IsRespondedMessage_WhenCorrelationIdNotFound_ThrowsKeyNotFoundException()
        {
            var mockConfig = new Mock<IQsMessagingConfiguration>();
            mockConfig.Setup(c => c.RequestResponseTimeout).Returns(60_000);
            var store = new RequestResponseMessageStore(_mockLogger.Object, mockConfig.Object);

            Assert.ThrowsException<KeyNotFoundException>(
                () => store.IsRespondedMessage("unknown-id"));
        }

        [TestMethod]
        public void GetRespondedMessage_WhenResponded_ReturnsCorrectMessage()
        {
            var mockConfig = new Mock<IQsMessagingConfiguration>();
            mockConfig.Setup(c => c.RequestResponseTimeout).Returns(60_000);
            var store = new RequestResponseMessageStore(_mockLogger.Object, mockConfig.Object);
            var response = new TestResponse { Name = "resp" };

            _ = store.AddRequestMessageAsync("id-1", new object(), CancellationToken.None);
            store.MarkAsResponded("id-1", response);

            var result = store.GetRespondedMessage<TestResponse>("id-1");

            Assert.AreEqual(response, result);
        }

        [TestMethod]
        public void GetRespondedMessage_WhenCorrelationIdNotFound_ThrowsKeyNotFoundException()
        {
            var mockConfig = new Mock<IQsMessagingConfiguration>();
            mockConfig.Setup(c => c.RequestResponseTimeout).Returns(60_000);
            var store = new RequestResponseMessageStore(_mockLogger.Object, mockConfig.Object);

            Assert.ThrowsException<KeyNotFoundException>(
                () => store.GetRespondedMessage<TestResponse>("unknown-id"));
        }

        [TestMethod]
        public void RemoveMessage_AfterAdding_CausesIsRespondedMessageToThrow()
        {
            var mockConfig = new Mock<IQsMessagingConfiguration>();
            mockConfig.Setup(c => c.RequestResponseTimeout).Returns(60_000);
            var store = new RequestResponseMessageStore(_mockLogger.Object, mockConfig.Object);

            _ = store.AddRequestMessageAsync("id-1", new object(), CancellationToken.None);
            store.RemoveMessage("id-1");

            Assert.ThrowsException<KeyNotFoundException>(
                () => store.IsRespondedMessage("id-1"));
        }

        [TestMethod]
        public async Task AddRequestMessageAsync_WhenTimeoutExpires_TaskFaultsWithTimeoutException()
        {
            var mockConfig = new Mock<IQsMessagingConfiguration>();
            mockConfig.Setup(c => c.RequestResponseTimeout).Returns(50); // 50 ms
            var store = new RequestResponseMessageStore(_mockLogger.Object, mockConfig.Object);

            var task = store.AddRequestMessageAsync("id-timeout", new object(), CancellationToken.None);

            await Assert.ThrowsExceptionAsync<TimeoutException>(() => task);
        }
    }
}
