using System.Diagnostics.CodeAnalysis;
using GrpcGreeter.RabbitGrpc.Shared.Server;

namespace GrpcGreeter.RabbitGrpc.Server.Internal.CallHandlers;

internal class DuplexStreamingServerCallHandler<
#if NET5_0_OR_GREATER
    [DynamicallyAccessedMembers(RabbitGrpcProtocolConstants.ServiceAccessibility)]
#endif
    TService, TRequest, TResponse> : ServerCallHandlerBase<TService, TRequest, TResponse>
    where TRequest : class
    where TResponse : class
    where TService : class
{
    private readonly DuplexStreamingServerMethodInvoker<TService, TRequest, TResponse> _invoker;

    public DuplexStreamingServerCallHandler(
        DuplexStreamingServerMethodInvoker<TService, TRequest, TResponse> invoker,
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