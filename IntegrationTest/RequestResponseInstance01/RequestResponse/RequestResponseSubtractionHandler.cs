using QsMessaging.Public.Handler;
using TestContract.RequestResponse;

namespace RequestResponseInstance01.RequestResponse
{
    internal class RequestResponseSubtractionHandler : IQsRequestResponseHandler<RRRequestSubtractionContract, RRResponseSubtractionContract>
    {
        public Task<RRResponseSubtractionContract> Consumer(RRRequestSubtractionContract request)
        {
            Console.WriteLine($"Instance 01: Request to SUBSTRACTION {request.Number1} - {request.Number2} ");
            return Task.FromResult(new RRResponseSubtractionContract() { SubtractionAnswer = request.Number1 - request.Number2 });
        }
    }
}
