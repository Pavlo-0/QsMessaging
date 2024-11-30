using QsMessaging.Public.Handler;
using QsMessaging.RabbitMq.Interfaces;
using QsMessaging.RabbitMq.Services;

namespace QsMessaging.RabbitMq
{
    internal static class HardConfiguration
    {
        public struct SupportedInterfacesStruct
        {
            public Type TypeInterface { get; set; }
            public QueueType Queue { get; init; }
            public ConsumerType Consumer { get; set; }

            public string Name
            {
                get
                {
                    return TypeInterface.FullName ?? throw new Exception("Error with HardConfiguration");
                }
            }
        }

        public static SupportedInterfacesStruct[] SupportedInterfaces
        {
            get
            {
                return new SupportedInterfacesStruct[]
                {
                    new SupportedInterfacesStruct
                    {
                        TypeInterface = typeof(IQsEventHandler<>),
                        Queue = QueueType.Temporary,
                        Consumer = ConsumerType.MessageEventConsumer
                    },
                    new SupportedInterfacesStruct
                    {
                        TypeInterface = typeof(IQsMessageHandler<>),
                        Queue = QueueType.Permanent,
                        Consumer = ConsumerType.MessageEventConsumer

                    },
                    new SupportedInterfacesStruct
                    {
                        TypeInterface = typeof(IRequestResponseResponseHandler),
                        Queue = QueueType.LiveTime,
                        Consumer = ConsumerType.RequestResponseResponseConsumer
                    },
                };
            }
        }

        public static Type[] SupportedInterfacesTypes
        {
            get
            {
                return SupportedInterfaces.Select(x => x.TypeInterface).ToArray();
            }
        }

        public static QueueType GetQueueByInterfaceTypes(Type interfaceType)
        {
            return SupportedInterfaces.First(x => x.TypeInterface == interfaceType).Queue;
        }

        public static ConsumerType GetConsumerByInterfaceTypes(Type interfaceType)
        {
            return SupportedInterfaces.First(x => x.TypeInterface == interfaceType).Consumer;
        }
    }
}