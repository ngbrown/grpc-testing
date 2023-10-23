using Grpc.Core;

namespace GrpcGreeter.Services;

public class FibonacciService : Fibonacci.FibonacciBase
{
    private readonly ILogger<FibonacciService> _logger;
    public FibonacciService(ILogger<FibonacciService> logger)
    {
        _logger = logger;
    }

    public override Task<FibonacciReply> Fib(FibonacciRequest request, ServerCallContext context)
    {
        if (request.Max is < 0 or > 46)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Max out of range"));
        }

        var result = Fib(request.Max, context.CancellationToken);

        return Task.FromResult(new FibonacciReply { Number = (ulong)result });
    }

    /// <remarks>
    /// Assumes only valid positive integer input.
    /// Don't expect this one to work for big numbers, and it's probably the slowest recursive implementation possible.
    /// </remarks>
    private static int Fib(int n, CancellationToken cancellationToken)
    {
        if (n is 0 or 1)
        {
            return n;
        }

        cancellationToken.ThrowIfCancellationRequested();

        return Fib(n - 1, cancellationToken) + Fib(n - 2, cancellationToken);
    }
}