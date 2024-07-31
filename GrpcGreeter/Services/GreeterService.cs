using Grpc.Core;
using GrpcGreeter;

namespace GrpcGreeter.Services
{
    public class GreeterService : Greeter.GreeterBase
    {
        private readonly ILogger<GreeterService> _logger;
        public GreeterService(ILogger<GreeterService> logger)
        {
            _logger = logger;
        }

        public override Task<HelloReply> SayHello(HelloRequest request, ServerCallContext context)
        {
            if (string.Equals(request.Name, "nobody", StringComparison.InvariantCultureIgnoreCase))
            {
                throw new RpcException(new Status(StatusCode.PermissionDenied,
                    $"Unable to say hello to '{request.Name}'"));
            }

            return Task.FromResult(new HelloReply
            {
                Message = "Hello " + request.Name
            });
        }
    }
}