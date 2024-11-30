
using QsMessaging.RabbitMq;
using QsMessaging.RabbitMq.Services;

namespace QsMessagingUnitTests
{
    [TestClass]
    public class NameGeneratorTest
    {
        [TestMethod]
        public void GetExchangeNameFromType_ReturnsCorrectExchangeName_WhenTypeIsProvided()
        {
            var nameGenerator = new NameGenerator();
            var result = nameGenerator.GetExchangeNameFromType(typeof(TestClass));
            Assert.AreEqual("Qs:QsMessagingUnitTests.NameGeneratorTest+TestClass:ex", result);
        }

        [TestMethod]
        public void GetExchangeNameFromType1_ReturnsCorrectExchangeName_WhenTypeIsProvided()
        {
            var nameGenerator = new NameGenerator();
            var result = nameGenerator.GetExchangeNameFromType(typeof(TestLongLongLongNameClass));
            Assert.AreEqual("Qs:QsMessagingUnitTests.NameGeneratorTest+TestLongLongLongNameClass:ex", result);
        }

        [TestMethod]
        public void GetExchangeNameFromType2_ReturnsCorrectExchangeName_WhenTypeIsProvided()
        {
            var nameGenerator = new NameGenerator();
            var result = nameGenerator.GetExchangeNameFromType(
                typeof(TestLongLongLongNameClassTestLongLongLongNameClass<TestLongLongLongNameClass, TestLongLongLongNameClass, TestLongLongLongNameClass>));
            Assert.AreEqual("Qs:df42a16c260d039d4c6b837c060eb84d81b8be982c58979b55c5ed1f62326d30:ex", result);
        }

        [TestMethod]
        public void GetQueueNameFromType1_ReturnsCorrectQueueName_WhenTypeAndQueueTypeIsProvided()
        {
            var nameGenerator = new NameGenerator();
            var result = nameGenerator.GetQueueNameFromType(typeof(TestClass), QueueType.Permanent);
            Assert.AreEqual("Qs:QsMessagingUnitTests.NameGeneratorTest+TestClass:permanent", result);
        }

        [TestMethod]
        public void GetQueueNameFromType2_ReturnsCorrectQueueName_WhenTypeAndQueueTypeIsProvided()
        {
            var nameGenerator = new NameGenerator();
            var result = nameGenerator.GetQueueNameFromType(typeof(TestLongLongLongNameClass), QueueType.Permanent);
            Assert.AreEqual("Qs:QsMessagingUnitTests.NameGeneratorTest+TestLongLongLongNameClass:permanent", result);
        }

        [TestMethod]
        public void GetQueueNameFromType3_ReturnsCorrectQueueName_WhenTypeAndQueueTypeIsProvided()
        {
            var nameGenerator = new NameGenerator();
            var result = nameGenerator.GetQueueNameFromType(
                typeof(TestLongLongLongNameClassTestLongLongLongNameClass<TestLongLongLongNameClass, TestLongLongLongNameClass, TestLongLongLongNameClass>), QueueType.Permanent);
            Assert.AreEqual("Qs:df42a16c260d039d4c6b837c060eb84d81b8be982c58979b55c5ed1f62326d30:permanent", result);
        }

        [TestMethod]
        public void GetExchangeNameFromType_ThrowsArgumentNullException_WhenTypeIsNull()
        {
            var nameGenerator = new NameGenerator();
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
            Assert.ThrowsException<ArgumentNullException>(() => nameGenerator.GetExchangeNameFromType(null));
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
        }

        [TestMethod]
        public void GetQueueNameFromType_ThrowsArgumentNullException_WhenTypeIsNull()
        {
            var nameGenerator = new NameGenerator();
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
            Assert.ThrowsException<ArgumentNullException>(() => nameGenerator.GetQueueNameFromType(null, QueueType.Permanent));
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
        }

        [TestMethod]
        public void GetQueueNameFromType_ThrowsArgumentException_WhenQueueTypeIsInvalid()
        {
            var nameGenerator = new NameGenerator();
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => nameGenerator.GetQueueNameFromType(typeof(string), (QueueType)999));
        }

        public class TestClass { }

        public class TestLongLongLongNameClass { }

        public class TestLongLongLongNameClassTestLongLongLongNameClass<T1, T2,T3> { }
    }
}
