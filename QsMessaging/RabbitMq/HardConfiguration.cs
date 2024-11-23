using QsMessaging.Public.Handler;

namespace QsMessaging.RabbitMq
{
    internal static class HardConfiguration
    {
        public struct SupportedInterfacesStruct
        {
            public Type TypeInterface { get; set; }
            public QueueType Queue { get; init; }
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
                        Queue = QueueType.Temporary
                    },
                    new SupportedInterfacesStruct
                    {
                        TypeInterface = typeof(IQsMessageHandler<>),
                        Queue = QueueType.Permanent
                    }
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
    }
}