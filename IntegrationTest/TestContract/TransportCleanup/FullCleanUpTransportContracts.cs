namespace TestContract.TransportCleanup;

public sealed record FullCleanUpTransportMessageContract(string Id);

public sealed record FullCleanUpTransportEventContract(string Id);

public sealed record FullCleanUpTransportRequestContract(string Id);

public sealed record FullCleanUpTransportResponseContract(string Id, string Status);
