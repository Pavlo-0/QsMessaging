namespace TestContract.TransportCleanup;

public sealed record CleanUpTransportMessageContract(string Id);

public sealed record CleanUpTransportEventContract(string Id);

public sealed record CleanUpTransportRequestContract(string Id);

public sealed record CleanUpTransportResponseContract(string Id, string Status);
