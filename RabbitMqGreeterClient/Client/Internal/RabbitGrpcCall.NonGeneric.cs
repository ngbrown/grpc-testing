using Grpc.Core;
using Microsoft.Extensions.Logging;
using RabbitRpc.Core;

namespace RabbitMqGreeterClient.Client.Internal;

internal abstract class RabbitGrpcCall
{
    // Getting logger name from generic type is slow
    private const string LoggerName = "RabbitMqGreeterClient.Client.Internal.GrpcCall";

    protected RabbitGrpcCall(CallOptions options, RabbitRpcChannel channel)
    {
        Options = options;
        Channel = channel;
        Logger = channel.LoggerFactory.CreateLogger(LoggerName);
    }

    public bool ResponseFinished { get; protected set; }
    public RabbitRpcResponse RpcResponse { get; protected set; }

    public CallOptions Options { get; }
    public ILogger Logger { get; }
    public RabbitRpcChannel Channel { get; }

    public abstract Task<Status> CallTask { get; }
    public abstract CancellationToken CancellationToken { get; }


    public CancellationToken GetCanceledToken(CancellationToken methodCancellationToken)
    {
        if (methodCancellationToken.IsCancellationRequested)
        {
            return methodCancellationToken;
        }
        else if (Options.CancellationToken.IsCancellationRequested)
        {
            return Options.CancellationToken;
        }
        else if (CancellationToken.IsCancellationRequested)
        {
            return CancellationToken;
        }
        return CancellationToken.None;
    }

    protected Status? ValidateHeaders(RabbitRpcResponse rpcResponse)
    {
        Status? status;
        status = new Status((StatusCode)(rpcResponse.Status?.Code ?? 0), rpcResponse.Status?.Message ?? string.Empty);
        return status;
    }
}