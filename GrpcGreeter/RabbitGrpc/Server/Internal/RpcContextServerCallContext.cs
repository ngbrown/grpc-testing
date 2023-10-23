using Grpc.AspNetCore.Server;
using Grpc.Core;
using GrpcGreeter.RabbitGrpc.Shared.Server;

namespace GrpcGreeter.RabbitGrpc.Server.Internal;

internal class RpcContextServerCallContext : ServerCallContext, IServerCallContextFeature
{
    internal RpcContextServerCallContext(RpcContext rpcContext, MethodOptions options, Type requestType, Type responseType, ILogger logger)
    {
        RpcContext = rpcContext;
        Options = options;
        RequestType = requestType;
        ResponseType = responseType;
        Logger = logger;
    }

    internal ILogger Logger { get; }
    internal RpcContext RpcContext { get; }
    internal MethodOptions Options { get; }
    internal Type RequestType { get; }
    internal Type ResponseType { get; }

    protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders)
    {
        ArgumentNullException.ThrowIfNull(responseHeaders);

        throw new NotImplementedException();
    }

    protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options)
    {
        throw new NotImplementedException("CreatePropagationToken will be implemented in a future version.");
    }

    protected override string MethodCore { get; }
    protected override string HostCore { get; }
    protected override string PeerCore { get; }
    protected override DateTime DeadlineCore { get; }
    protected override Metadata RequestHeadersCore { get; }
    protected override CancellationToken CancellationTokenCore => RpcContext.RequestAborted;
    protected override Metadata ResponseTrailersCore { get; }
    protected override Status StatusCore { get; set; }
    protected override WriteOptions? WriteOptionsCore { get; set; }
    protected override AuthContext AuthContextCore { get; }
    public ServerCallContext ServerCallContext => this;

    public void Initialize()
    {
    }

    public Task ProcessHandlerErrorAsync(Exception ex, string methodName)
    {
        if (ex is RpcException rpcException)
        {
            Status = rpcException.Status;
        }
        else
        {
            Status = new Status(StatusCode.Unknown, "Exception was thrown by handler. " + ex.Message);
        }

        RpcContext.Response.Status = new RabbitRpc.Core.Google.Status
            { Code = (int)Status.StatusCode, Message = Status.Detail, };

        return Task.CompletedTask;
    }

    public Task EndCallAsync()
    {
        return Task.CompletedTask;
    }
}