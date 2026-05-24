using QsMessaging.Shared;

namespace QsMessagingUnitTests.Shared
{
    [TestClass]
    public class TransportFullCleanupNameFilterTest
    {
        [DataTestMethod]
        [DataRow("Qs-Topic-Orders", false, true)]
        [DataRow("orders", false, false)]
        [DataRow("orders", true, true)]
        public void CanDeleteAzureServiceBusQueueOrTopic_UsesDangerousFlagOrQsPrefix(
            string entityName,
            bool allowDangerousFullCleanup,
            bool expected)
        {
            var actual = TransportFullCleanupNameFilter.CanDeleteAzureServiceBusQueueOrTopic(
                entityName,
                allowDangerousFullCleanup);

            Assert.AreEqual(expected, actual);
        }

        [DataTestMethod]
        [DataRow("Qs_P_abc", false, true)]
        [DataRow("external", false, false)]
        [DataRow("external", true, true)]
        public void CanDeleteAzureServiceBusSubscription_UsesDangerousFlagOrQsScope(
            string subscriptionName,
            bool allowDangerousFullCleanup,
            bool expected)
        {
            var actual = TransportFullCleanupNameFilter.CanDeleteAzureServiceBusSubscription(
                subscriptionName,
                allowDangerousFullCleanup);

            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void CanDeleteRabbitMqExchange_NeverDeletesReservedAmqExchanges()
        {
            Assert.IsFalse(TransportFullCleanupNameFilter.CanDeleteRabbitMqExchange("amq.direct", true));
        }
    }
}
