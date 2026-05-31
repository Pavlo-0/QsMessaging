using IntegrationTestV2.Common;
using IntegrationTestV2.Contracts;
using QsMessaging.Public.Handler;

namespace IntegrationTestV2.Receiver;

public sealed class ScaleRequestHandler(ServiceIdentity identity)
    : IQsRequestResponseHandler<ScaleRequest, ScaleResponse>
{
    public Task<ScaleResponse> Consumer(ScaleRequest request)
    {
        return Consumer(request, CancellationToken.None);
    }

    public async Task<ScaleResponse> Consumer(ScaleRequest request, CancellationToken cancellationToken = default)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(25), cancellationToken);

        return new ScaleResponse(
            request.RequestId,
            request.SenderId,
            identity.ServiceId,
            request.Number1 + request.Number2);
    }
}
