using System.Diagnostics.CodeAnalysis;
using Google.Protobuf;
using Grpc.Core;
using GrpcGreeter.RabbitGrpc.Shared.Server;
using RabbitRpc.Core;

namespace GrpcGreeter.RabbitGrpc.Server.Internal.CallHandlers;

internal class UnaryServerCallHandler<
#if NET5_0_OR_GREATER
    [DynamicallyAccessedMembers(RabbitGrpcProtocolConstants.ServiceAccessibility)]
#endif
    TService, TRequest, TResponse> : ServerCallHandlerBase<TService, TRequest, TResponse>
    where TRequest : class
    where TResponse : class
    where TService : class
{
    private readonly UnaryServerMethodInvoker<TService, TRequest, TResponse> _invoker;

    public UnaryServerCallHandler(
        UnaryServerMethodInvoker<TService, TRequest, TResponse> invoker,
        ILoggerFactory loggerFactory)
        : base(invoker, loggerFactory)
    {
        _invoker = invoker;
    }

    protected override async Task HandleCallAsyncCore(RpcContext rpcContext, RpcContextServerCallContext serverCallContext)
    {
        TRequest request = MethodInvoker.Method.RequestMarshaller.ContextualDeserializer.Invoke(
            new ByteStringDeserializationContext(rpcContext.Request.Body));

        var response = await _invoker.Invoke(rpcContext, serverCallContext, request);

        if (response == null)
        {
            // This is consistent with Grpc.Core when a null value is returned
            throw new RpcException(new Status(StatusCode.Cancelled, "No message returned from method."));
        }

        // Check if deadline exceeded while method was invoked. If it has then skip trying to write
        // the response message because it will always fail.
        // Note that the call is still going so the deadline could still be exceeded after this point.
        // TODO

        rpcContext.Response.Body = WriteContent(response);
    }

    private ByteString? WriteContent(TResponse response)
    {
        ByteString? content = null;

        MethodInvoker.Method.ResponseMarshaller.ContextualSerializer(response, new BodyStringSerializationContext(SetContent));

        return content;

        void SetContent(ByteString newContent)
        {
            content = newContent;
        }
    }
}