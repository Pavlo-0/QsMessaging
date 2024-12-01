using QsMessaging.Public.Handler;
using TestContract.RequestResponse;

namespace ArrangeInstance01.RequestResponse
{
    internal class RequestResponseHandler : IQsRequestResponseHandler<RRRequestContract, RRResponseContract>
    {
        public Task<RRResponseContract> Consumer(RRRequestContract request)
        {
            return Task.FromResult(new RRResponseContract() { SumAnswer = request.Number1 + request.Number2 });
        }
    }
}
