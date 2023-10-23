using System.Diagnostics.CodeAnalysis;
using Grpc.Core;
using GrpcGreeter.RabbitGrpc.Shared.Server;

namespace GrpcGreeter.RabbitGrpc.Server.Internal.CallHandlers;

internal abstract class ServerCallHandlerBase<
#if NET5_0_OR_GREATER
    [DynamicallyAccessedMembers(RabbitGrpcProtocolConstants.ServiceAccessibility)]
#endif
    TService, TRequest, TResponse>
    where TService : class
    where TRequest : class
    where TResponse : class
{
    private const string LoggerName = "RabbitGrpc.Server.ServerCallHandler";

    protected ServerMethodInvokerBase<TService, TRequest, TResponse> MethodInvoker { get; }
    protected ILogger Logger { get; }

    protected ServerCallHandlerBase(
        ServerMethodInvokerBase<TService, TRequest, TResponse> methodInvoker,
        ILoggerFactory loggerFactory)
    {
        MethodInvoker = methodInvoker;
        Logger = loggerFactory.CreateLogger(LoggerName);
    }

    public Task HandleCallAsync(RpcContext rpcContext)
    {
        var serverCallContext = new RpcContextServerCallContext(rpcContext, MethodInvoker.Options, typeof(TRequest), typeof(TResponse), Logger);

        try
        {
            serverCallContext.Initialize();

            var handleCallTask = HandleCallAsyncCore(rpcContext, serverCallContext);

            if (handleCallTask.IsCompletedSuccessfully)
            {
                return serverCallContext.EndCallAsync();
            }
            else
            {
                return AwaitHandleCall(serverCallContext, MethodInvoker.Method, handleCallTask);
            }
        }
        catch (Exception ex)
        {
            return serverCallContext.ProcessHandlerErrorAsync(ex, MethodInvoker.Method.Name);
        }

        static async Task AwaitHandleCall(RpcContextServerCallContext serverCallContext, Method<TRequest, TResponse> method, Task handleCall)
        {
            try
            {
                await handleCall;
                await serverCallContext.EndCallAsync();
            }
            catch (Exception ex)
            {
                await serverCallContext.ProcessHandlerErrorAsync(ex, method.Name);
            }
        }
    }

    protected abstract Task HandleCallAsyncCore(RpcContext rpcContext, RpcContextServerCallContext serverCallContext);
}