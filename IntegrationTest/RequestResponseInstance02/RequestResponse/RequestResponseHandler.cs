using QsMessaging.Public.Handler;
using TestContract.RequestResponse;


namespace RequestResponseInstance02.RequestResponse
{
    internal class RequestResponseHandler : IQsRequestResponseHandler<RRRequestAddContract, RRResponseAddContract>
    {
        public Task<RRResponseAddContract> Consumer(RRRequestAddContract request)
        {
            Console.WriteLine($"Instance 02: Request to ADD {request.Number1} + {request.Number2} ");
            return Task.FromResult(new RRResponseAddContract() { SumAnswer = request.Number1 + request.Number2 });
        }
    }
}
