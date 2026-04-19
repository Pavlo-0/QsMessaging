using QsMessaging.Public.Handler;
using QsMessaging.RabbitMq.Models.Enums;
using QsMessaging.Shared;
using QsMessaging.Shared.Interface;

namespace QsMessagingUnitTests.RabbitMq
{
    [TestClass]
    public class HardConfigurationTest
    {
        [TestMethod]
        public void SupportedInterfaces_ContainsFourEntries()
        {
            Assert.AreEqual(4, HardConfiguration.SupportedInterfaces.Length);
        }

        [TestMethod]
        public void SupportedInterfacesTypes_ContainsAllExpectedTypes()
        {
            var types = HardConfiguration.SupportedInterfacesTypes;

            CollectionAssert.Contains(types, typeof(IQsEventHandler<>));
            CollectionAssert.Contains(types, typeof(IQsMessageHandler<>));
            CollectionAssert.Contains(types, typeof(IRRResponseHandler));
            CollectionAssert.Contains(types, typeof(IQsRequestResponseHandler<,>));
        }

        [TestMethod]
        public void GetExchangePurpose_ForIQsEventHandler_ReturnsTemporary()
        {
            var result = HardConfiguration.GetExchangePurpose(typeof(IQsEventHandler<>));

            Assert.AreEqual(RqExchangePurpose.Temporary, result);
        }

        [TestMethod]
        public void GetExchangePurpose_ForIQsMessageHandler_ReturnsPermanent()
        {
            var result = HardConfiguration.GetExchangePurpose(typeof(IQsMessageHandler<>));

            Assert.AreEqual(RqExchangePurpose.Permanent, result);
        }

        [TestMethod]
        public void GetQueuePurpose_ForIQsEventHandler_ReturnsConsumerTemporary()
        {
            var result = HardConfiguration.GetQueuePurpose(typeof(IQsEventHandler<>));

            Assert.AreEqual(RqQueuePurpose.ConsumerTemporary, result);
        }

        [TestMethod]
        public void GetQueuePurpose_ForIQsMessageHandler_ReturnsPermanent()
        {
            var result = HardConfiguration.GetQueuePurpose(typeof(IQsMessageHandler<>));

            Assert.AreEqual(RqQueuePurpose.Permanent, result);
        }

        [TestMethod]
        public void GetChannelPurpose_ForIQsEventHandler_ReturnsQueueConsumerTemporary()
        {
            var result = HardConfiguration.GetChannelPurpose(typeof(IQsEventHandler<>));

            Assert.AreEqual(RqChannelPurpose.QueueConsumerTemporary, result);
        }

        [TestMethod]
        public void GetChannelPurpose_ForIQsMessageHandler_ReturnsQueuePermanent()
        {
            var result = HardConfiguration.GetChannelPurpose(typeof(IQsMessageHandler<>));

            Assert.AreEqual(RqChannelPurpose.QueuePermanent, result);
        }

        [TestMethod]
        public void GetConsumerPurpose_ForIQsEventHandler_ReturnsMessageEventConsumer()
        {
            var result = HardConfiguration.GetConsumerPurpose(typeof(IQsEventHandler<>));

            Assert.AreEqual(RqConsumerPurpose.MessageEventConsumer, result);
        }

        [TestMethod]
        public void GetConsumerPurpose_ForIQsRequestResponseHandler_ReturnsRRRequestConsumer()
        {
            var result = HardConfiguration.GetConsumerPurpose(typeof(IQsRequestResponseHandler<,>));

            Assert.AreEqual(RqConsumerPurpose.RRRequestConsumer, result);
        }

        [TestMethod]
        public void GetConsumerPurpose_ForIRRResponseHandler_ReturnsRRResponseConsumer()
        {
            var result = HardConfiguration.GetConsumerPurpose(typeof(IRRResponseHandler));

            Assert.AreEqual(RqConsumerPurpose.RRResponseConsumer, result);
        }

        [TestMethod]
        public void GetConfigurationByInterfaceTypes_ForIQsMessageHandler_HasCorrectAllPurposes()
        {
            var result = HardConfiguration.GetConfigurationByInterfaceTypes(typeof(IQsMessageHandler<>));

            Assert.AreEqual(RqExchangePurpose.Permanent, result.ExchangePurpose);
            Assert.AreEqual(RqQueuePurpose.Permanent, result.QueuePurpose);
            Assert.AreEqual(RqChannelPurpose.QueuePermanent, result.ChannelPurpose);
            Assert.AreEqual(RqConsumerPurpose.MessageEventConsumer, result.ConsumerPurpose);
        }

        [TestMethod]
        public void GetConfigurationByInterfaceTypes_ForIQsEventHandler_HasCorrectAllPurposes()
        {
            var result = HardConfiguration.GetConfigurationByInterfaceTypes(typeof(IQsEventHandler<>));

            Assert.AreEqual(RqExchangePurpose.Temporary, result.ExchangePurpose);
            Assert.AreEqual(RqQueuePurpose.ConsumerTemporary, result.QueuePurpose);
            Assert.AreEqual(RqChannelPurpose.QueueConsumerTemporary, result.ChannelPurpose);
            Assert.AreEqual(RqConsumerPurpose.MessageEventConsumer, result.ConsumerPurpose);
        }
    }
}
