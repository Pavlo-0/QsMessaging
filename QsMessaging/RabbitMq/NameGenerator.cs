using QsMessaging.RabbitMq.Interface;
using QsMessaging.RabbitMq.Services;
using QsMessaging.RabbitMq.Services.Interfaces;
using System.Security.Cryptography;
using System.Text;

namespace QsMessaging.RabbitMq
{
    internal class NameGenerator(IInstanceService instanceService) : INameGenerator
    {
        public string GetExchangeNameFromType<TModel>()
        {
            return GenerateName(typeof(TModel), "ex");
        }

        public string GetExchangeNameFromType(Type TModel)
        {
            if (TModel is null)
            {
                throw new ArgumentNullException();
            }

            return GenerateName(TModel, "ex");
        }

        public string GetQueueNameFromType(Type TModel, QueueType queueType)
        {
            if (TModel is null)
            {
                throw new ArgumentNullException();
            }

            string banseQueueName = $"Qs:{TModel.FullName}";

            switch (queueType)
            {
                case QueueType.Permanent:
                    return GenerateName(TModel, "permanent");
                case QueueType.Temporary:
                    return GenerateName(TModel, Guid.NewGuid().ToString("N"));
                case QueueType.LiveTime:
                    return GenerateName(TModel, "livetime:" + instanceService.GetInstanceUID().ToString("N"));
                default:
                    throw new ArgumentOutOfRangeException("Unknown QueueType");
            }

        }
        private string GenerateName(Type type, string endName = "")
        {
            var fullName = type.FullName ?? "unknowType";
            return "Qs:" + (fullName.Length > 200 ? HashString(fullName) : fullName) + ":" + endName;
        }

        public static string HashString(string input, int maxLength = 200)
        {
            if (string.IsNullOrEmpty(input))
            {
                throw new ArgumentException("Input string cannot be null or empty.");
            }

            if (maxLength <= 0)
            {
                throw new ArgumentException("Maximum length must be greater than 0.");
            }

            using (var sha256 = SHA256.Create())
            {
                byte[] inputBytes = Encoding.UTF8.GetBytes(input);
                byte[] hashBytes = sha256.ComputeHash(inputBytes);

                var hashString = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();

                return hashString.Length > maxLength ? hashString.Substring(0, maxLength) : hashString;
            }
        }
    }

}
