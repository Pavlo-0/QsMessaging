using IntegrationTestV2.Contracts;
using IntegrationTestV2.Receiver;

await ReceiverHost.RunAsync(args, ServiceIds.Receiver02);
