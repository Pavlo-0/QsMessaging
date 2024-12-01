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
            public ChannelPurpose ChannelPurpose { get; init; }
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
                        Queue = QueueType.ConsumerTemporary,
                        ChannelPurpose = ChannelPurpose.QueueConsumerTemporary,
                        Consumer = ConsumerType.MessageEventConsumer
                    },
                    new SupportedInterfacesStruct
                    {
                        TypeInterface = typeof(IQsMessageHandler<>),
                        Queue = QueueType.Permanent,
                        ChannelPurpose = ChannelPurpose.QueuePermanent,
                        Consumer = ConsumerType.MessageEventConsumer

                    },
                    new SupportedInterfacesStruct
                    {
                        TypeInterface = typeof(IRequestResponseResponseHandler),
                        Queue = QueueType.InstanceTemporary,
                        ChannelPurpose = ChannelPurpose.QueueInstanceTemporary,
                        Consumer = ConsumerType.RequestResponseResponseConsumer
                    },
                    new SupportedInterfacesStruct
                                        {
                        TypeInterface = typeof(IQsRequestResponseHandler<,>),
                        Queue = QueueType.SingleTemporary,
                        ChannelPurpose = ChannelPurpose.QueueSingleTemporary,
                        //Consumer = ConsumerType.RequestResponseRequestConsumer
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

        public static ChannelPurpose GetChannelPurposeByInterfaceTypes(Type interfaceType)
        {
            return SupportedInterfaces.First(x => x.TypeInterface == interfaceType).ChannelPurpose;
        }

        public static ConsumerType GetConsumerByInterfaceTypes(Type interfaceType)
        {
            return SupportedInterfaces.First(x => x.TypeInterface == interfaceType).Consumer;
        }
    }
}