using QsMessaging.RabbitMq.Interface;

namespace QsMessaging.RabbitMq
{
    internal class ExchangeNameGenerator : IExchangeNameGenerator
    {
        public string GetExchangeNameFromType<TModel>()
        {
            return GenerateName(typeof(TModel));
        }

        public string GetExchangeNameFromType(Type TModel)
        {
            return GenerateName(TModel);
        }

        public string GetQueueNameFromType(Type TModel)
        {
            return $"permanent_{GenerateName(TModel)}";
        }

        public string GetQueueTemporaryNameFromType(Type TModel)
        {
            return $"temporary_{GenerateName(TModel)}_{Guid.NewGuid().ToString("N")}";
        }

        private string GenerateName(Type type)
        {
            return "ex_" + type.FullName;
        }
    }

}
