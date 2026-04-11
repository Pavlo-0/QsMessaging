using QsMessaging.AzureServiceBus.Services;

namespace QsMessagingUnitTests.AzureServiceBus.Services
{
    [TestClass]
    public class ServiceBusEntityNameFormatterTest
    {
        [TestMethod]
        public void FormatQueueName_WhenNameContainsUnsupportedCharacters_ReturnsServiceBusSafeName()
        {
            var rawName = "Qs:TestContract.RequestResponse.RRRequest2InstanceRequestContract:livetime";

            var result = ServiceBusEntityNameFormatter.FormatQueueName(rawName);

            Assert.IsTrue(result.Length <= 260);
            Assert.IsTrue(IsSafe(result));
            Assert.IsFalse(result.Contains(':'));
        }

        [TestMethod]
        public void FormatQueueName_WhenCalledTwice_ReturnsSameName()
        {
            var rawName = "Qs:Nested+Contract:livetime";

            var once = ServiceBusEntityNameFormatter.FormatQueueName(rawName);
            var twice = ServiceBusEntityNameFormatter.FormatQueueName(once);

            Assert.AreEqual(once, twice);
        }

        [TestMethod]
        public void FormatQueueName_WhenSanitizedBaseMatchesAnotherValidName_RemainsUnique()
        {
            var first = ServiceBusEntityNameFormatter.FormatQueueName("Qs:Contract:livetime");
            var second = ServiceBusEntityNameFormatter.FormatQueueName("Qs.Contract.livetime");

            Assert.AreNotEqual(first, second);
        }

        [TestMethod]
        public void FormatSubscriptionName_WhenNameIsTooLong_ReturnsSafeNameWithinSubscriptionLimit()
        {
            var rawName = $"Qs:sub:{new string('a', 160)}";

            var result = ServiceBusEntityNameFormatter.FormatSubscriptionName(rawName);

            Assert.IsTrue(result.Length <= 50);
            Assert.IsTrue(IsSafe(result));
        }

        private static bool IsSafe(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            if (!IsAsciiLetterOrDigit(value[0]) || !IsAsciiLetterOrDigit(value[^1]))
            {
                return false;
            }

            return value.All(symbol =>
                IsAsciiLetterOrDigit(symbol) || symbol is '.' or '-' or '_');
        }

        private static bool IsAsciiLetterOrDigit(char symbol)
        {
            return (symbol >= 'a' && symbol <= 'z')
                || (symbol >= 'A' && symbol <= 'Z')
                || (symbol >= '0' && symbol <= '9');
        }
    }
}
