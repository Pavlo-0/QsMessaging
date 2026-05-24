using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Hosting;
using QsMessaging.AzureServiceBus.Services.Interfaces;
using QsMessaging.AzureServiceBus;
using QsMessaging.AzureServiceBus.Services;
using QsMessaging.RabbitMq;
using QsMessaging.RabbitMq.Services;
using QsMessaging.RabbitMq.Services.Interfaces;
using QsMessaging.RabbitMq.Interfaces;
using QsMessaging.Shared.Interface;
using QsMessaging.Shared.Services.Interfaces;
using QsMessaging.Shared.Services;
using QsMessaging.Shared;
using Polly;
using Polly.Retry;
using System.Reflection;
using System.Runtime.CompilerServices;


namespace QsMessaging.Public
{
    public static class QsMessagingRegistering
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static IServiceCollection AddQsMessaging(this IServiceCollection services, Action<IQsMessagingConfiguration> options)
        {
            return AddQsMessagingCore(services, options, Assembly.GetCallingAssembly(), Array.Empty<Assembly>());
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static IServiceCollection AddQsMessaging(this IServiceCollection services, Action<IQsMessagingConfiguration> options, params Assembly[] assembliesToScan)
        {
            return AddQsMessagingCore(services, options, Assembly.GetCallingAssembly(), assembliesToScan);
        }

        private static IServiceCollection AddQsMessagingCore(
            IServiceCollection services,
            Action<IQsMessagingConfiguration> options,
            Assembly callingAssembly,
            Assembly[] assembliesToScan)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(options);

            var configuration = new Configuration();
            options(configuration);
            ValidateConfiguration(configuration);
            assembliesToScan ??= Array.Empty<Assembly>();
            var scanAssemblies = ResolveAssembliesToScan(configuration, callingAssembly, assembliesToScan);
            var explicitScanAssemblies =
                configuration.AssembliesToScan.Any(assembly => assembly is not null) ||
                assembliesToScan.Any(assembly => assembly is not null);
            var handlerGeneratorInstance = new HandlerService(services, scanAssemblies);

            if (explicitScanAssemblies && handlerGeneratorInstance.DiscoveredConsumerHandlerCount == 0)
            {
                var assemblyNames = string.Join(", ", scanAssemblies.Select(assembly => assembly.GetName().Name));
                throw new InvalidOperationException($"No QsMessaging handlers were found in the configured scan assemblies: {assemblyNames}.");
            }

            services.AddSingleton<IQsMessagingConfiguration>(configuration);

            services.AddTransient<IInstanceService, InstanceService>();
            services.AddTransient<IQsMessaging, QsMessagingGate>();
            services.AddTransient<IHandlerService>(hg=>
            {
                return handlerGeneratorInstance;
            });

            handlerGeneratorInstance.RegisterAllHandlers();
            services.AddTransient<IRequestResponseMessageStore , RequestResponseMessageStore>();

            services.AddTransient(typeof(Lazy<>), typeof(LazyService<>));
            RegisterTransportServices(services, configuration);

            return services;
        }

        private static IReadOnlyCollection<Assembly> ResolveAssembliesToScan(
            IQsMessagingConfiguration configuration,
            Assembly callingAssembly,
            IEnumerable<Assembly> assembliesToScan)
        {
            var configuredAssemblies = configuration.AssembliesToScan
                .Concat(assembliesToScan)
                .Where(assembly => assembly is not null)
                .Distinct()
                .ToArray();

            if (configuredAssemblies.Length > 0)
            {
                return configuredAssemblies;
            }

            var defaultAssemblies = new List<Assembly>();
            var entryAssembly = Assembly.GetEntryAssembly();

            if (entryAssembly is not null)
            {
                defaultAssemblies.Add(entryAssembly);
            }

            if (!defaultAssemblies.Contains(callingAssembly))
            {
                defaultAssemblies.Add(callingAssembly);
            }

            if (defaultAssemblies.Count == 0)
            {
                throw new InvalidOperationException("No assembly is available for QsMessaging handler discovery. Configure options.AssembliesToScan or use AddQsMessaging(..., params Assembly[]).");
            }

            return defaultAssemblies;
        }

        public static async Task<IHost> UseQsMessaging(this IHost host)
        {
            var manager = host.Services.GetRequiredService<IQsMessagingConnectionManager>();
            await manager.Open();
           
            return host;
        }

        /// <summary>
        /// Stops the current transport connection and removes messaging entities created by the configured QsMessaging transport.
        /// Intended for debug or local reset scenarios before opening the transport again.
        /// </summary>
        public static async Task<IHost> CleanUpTransportation(this IHost host, CancellationToken cancellationToken = default)
        {
            var manager = host.Services.GetRequiredService<IQsMessagingConnectionManager>();
            await manager.Close(cancellationToken);

            var cleaner = host.Services.GetRequiredService<IQsMessagingTransportCleaner>();
            await cleaner.CleanUp(cancellationToken);

            return host;
        }

        /// <summary>
        /// Stops the current transport connection and removes all messaging entities visible to the configured transport scope.
        /// Intended for debug or local reset scenarios.
        /// </summary>
        public static async Task<IHost> FullCleanUpTransportation(this IHost host, CancellationToken cancellationToken = default)
        {
            var manager = host.Services.GetRequiredService<IQsMessagingConnectionManager>();
            await manager.Close(cancellationToken);

            var cleaner = host.Services.GetRequiredService<IQsMessagingTransportFullCleaner>();
            await cleaner.FullCleanUp(cancellationToken);

            return host;
        }

        private static void RegisterTransportServices(IServiceCollection services, IQsMessagingConfiguration configuration)
        {
            services.AddTransient<IConsumerService, ConsumerService>();   

            switch (configuration.Transport)
            {
                case QsMessagingTransport.RabbitMq:

                    RegisterRabbitMqManagementHttpClient(services, configuration);

                    services.AddSingleton<IRqConnectionService, RbConnectionService>();
                    services.AddTransient<ISubscriber, RqSubscriber>();
                    services.AddTransient<IQsMessagingConnectionManager, RqConnectionManager>();
                    services.AddTransient<IQsMessagingTransportCleaner, RqTransportCleaner>();
                    services.AddTransient<IQsMessagingTransportFullCleaner, RqTransportFullCleaner>();

                    services.AddTransient<ISender, RqSender>();
                    services.AddTransient<IRqNameGenerator, RqNameGenerator>();
                    services.AddTransient<IRqManagementService, RqManagementService>();


                    services.AddTransient<IRqExchangeService, RqExchangeService>();
                    services.AddTransient<IRqChannelService, RqChannelService>();
                    services.AddTransient<IRqQueueService, RqQueueService>();
                    services.AddTransient<IRqConsumerService, RqConsumerService>();
                    break;

                case QsMessagingTransport.AzureServiceBus:
                    services.AddSingleton<IAsbConnectionService, AsbConnectionService>();
                    services.AddTransient<ISubscriber, AsbSubscriber>();
                    services.AddTransient<IQsMessagingConnectionManager, AsbConnectionManager>();
                    services.AddTransient<IQsMessagingTransportCleaner, AsbTransportCleaner>();
                    services.AddTransient<IQsMessagingTransportFullCleaner, AsbTransportFullCleaner>();


                    services.AddTransient<ISender, AsbSender>();
                    services.AddTransient<IAsbNameGeneratorService, AsbNameGenerator>();

                    services.AddTransient<IAsbQueueService, AsbQueueService>();
                    services.AddTransient<IAsbTopicService, AsbTopicService>();
                    services.AddTransient<IAsbTopicSubscriptionService, AsbTopicSubscriptionService>();

                    services.AddTransient<IAsbServiceBusProcessorService, AsbServiceBusProcessorService>();
                    services.AddTransient<IAsbConsumerService, AsbConsumerService>();
                    break;

                default:
                    throw new NotSupportedException($"The transport '{configuration.Transport}' is not supported.");
            }
        }

        private static void RegisterRabbitMqManagementHttpClient(IServiceCollection services, IQsMessagingConfiguration configuration)
        {
            var resilience = configuration.Resilience;
            var httpClientBuilder = services.AddHttpClient(RqManagementService.HttpClientName);

            if (resilience.MaxRetryAttempts == 0)
            {
                return;
            }

            httpClientBuilder.AddResilienceHandler("RabbitMqManagement", builder =>
            {
                builder.AddRetry(new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = resilience.MaxRetryAttempts,
                    Delay = resilience.Delay,
                    BackoffType = resilience.BackoffType,
                    UseJitter = resilience.UseJitter,
                    ShouldRetryAfterHeader = true,
                    ShouldHandle = args =>
                    {
                        return ValueTask.FromResult(HttpClientResiliencePredicates.IsTransient(args.Outcome));
                    }
                });
            });
        }

        private static void ValidateConfiguration(IQsMessagingConfiguration configuration)
        {
            ValidateRetryConfiguration(configuration.Resilience, "Resilience");
            ValidateRetryConfiguration(configuration.HandlerResilience, "HandlerResilience");

            if (configuration.Transport != QsMessagingTransport.AzureServiceBus)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(configuration.AzureServiceBus.ConnectionString))
            {
                throw new InvalidOperationException("Azure Service Bus transport requires AzureServiceBus.ConnectionString to be configured.");
            }
        }

        private static void ValidateRetryConfiguration(QsMessageReceiverRetryConfiguration resilience, string optionName)
        {
            if (resilience.MaxRetryAttempts < 0)
            {
                throw new InvalidOperationException($"{optionName}.MaxRetryAttempts can not be negative.");
            }

            if (resilience.Delay < TimeSpan.Zero)
            {
                throw new InvalidOperationException($"{optionName}.Delay can not be negative.");
            }
        }
    }
}
