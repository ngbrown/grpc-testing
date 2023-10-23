using System.Diagnostics;
using Grpc.Core;

namespace RabbitMqGreeterClient.Client.Internal;

internal class RabbitRpcCallInvoker : CallInvoker
{
    private readonly RabbitRpcChannel _channel;

    public RabbitRpcCallInvoker(RabbitRpcChannel channel)
    {
        _channel = channel;
    }

    public override TResponse BlockingUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host,
        CallOptions options, TRequest request)
    {
        throw new NotImplementedException();
    }

    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method,
        string? host, CallOptions options, TRequest request)
    {
        var call = CreateRootRabbitGrpcCall<TRequest, TResponse>(this._channel, method, options);
        call.StartUnary(request);

        var callWrapper = new AsyncUnaryCall<TResponse>(
            responseAsync: call.GetResponseAsync(),
            responseHeadersAsync: Callbacks<TRequest, TResponse>.GetResponseHeadersAsync,
            getStatusFunc: Callbacks<TRequest, TResponse>.GetStatus,
            getTrailersFunc: Callbacks<TRequest, TResponse>.GetTrailers,
            disposeAction: Callbacks<TRequest, TResponse>.Dispose,
            call);

        return callWrapper;
    }

    public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
        Method<TRequest, TResponse> method, string? host, CallOptions options,
        TRequest request)
    {
        throw new NotImplementedException();
    }

    public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(
        Method<TRequest, TResponse> method, string? host, CallOptions options)
    {
        throw new NotImplementedException();
    }

    public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(
        Method<TRequest, TResponse> method, string? host, CallOptions options)
    {
        throw new NotImplementedException();
    }
    
    [Conditional("ASSERT_METHOD_TYPE")]
    private static void AssertMethodType(IMethod method, MethodType methodType)
    {
        // This can be used to assert tests are passing the right method type.
        if (method.Type != methodType)
        {
            throw new Exception("Expected method type: " + methodType);
        }
    }

    private static IRabbitGrpcCall<TRequest, TResponse> CreateRootRabbitGrpcCall<TRequest, TResponse>(
        RabbitRpcChannel channel,
        Method<TRequest, TResponse> method,
        CallOptions options)
        where TRequest : class
        where TResponse : class
    {
        ObjectDisposedException.ThrowIf(channel.Disposed, typeof(RabbitRpcChannel));

        var call = new RabbitGrpcCall<TRequest, TResponse>(method, options, channel, attemptCount: 1);
        call.CallWrapper = null;

        return call;
    }

    // Store static callbacks so delegates are allocated once
    private static class Callbacks<TRequest, TResponse>
        where TRequest : class
        where TResponse : class
    {
        internal static readonly Func<object, Task<Metadata>> GetResponseHeadersAsync = state => ((IRabbitGrpcCall<TRequest, TResponse>)state).GetResponseHeadersAsync();
        internal static readonly Func<object, Status> GetStatus = state => ((IRabbitGrpcCall<TRequest, TResponse>)state).GetStatus();
        internal static readonly Func<object, Metadata> GetTrailers = state => ((IRabbitGrpcCall<TRequest, TResponse>)state).GetTrailers();
        internal static readonly Action<object> Dispose = state => ((IRabbitGrpcCall<TRequest, TResponse>)state).Dispose();
    }
}