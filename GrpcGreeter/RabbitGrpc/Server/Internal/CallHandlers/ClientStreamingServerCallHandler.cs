using System.Diagnostics.CodeAnalysis;
using GrpcGreeter.RabbitGrpc.Shared.Server;

namespace GrpcGreeter.RabbitGrpc.Server.Internal.CallHandlers;

internal class ClientStreamingServerCallHandler<
#if NET5_0_OR_GREATER
    [DynamicallyAccessedMembers(RabbitGrpcProtocolConstants.ServiceAccessibility)]
#endif
    TService, TRequest, TResponse> : ServerCallHandlerBase<TService, TRequest, TResponse>
    where TRequest : class
    where TResponse : class
    where TService : class
{
    private readonly ClientStreamingServerMethodInvoker<TService, TRequest, TResponse> _invoker;

    public ClientStreamingServerCallHandler(
        ClientStreamingServerMethodInvoker<TService, TRequest, TResponse> invoker,
        ILoggerFactory loggerFactory)
        : base(invoker, loggerFactory)
    {
        _invoker = invoker;
    }

    protected override Task HandleCallAsyncCore(RpcContext rpcContext, RpcContextServerCallContext serverCallContext)
    {
        throw new NotImplementedException();
    }
}