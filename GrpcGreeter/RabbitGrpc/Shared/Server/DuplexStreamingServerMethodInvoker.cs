using System.Diagnostics.CodeAnalysis;
using Grpc.AspNetCore.Server;
using Grpc.AspNetCore.Server.Model;
using Grpc.Core;
using GrpcGreeter.RabbitGrpc.Server.Internal;

namespace GrpcGreeter.RabbitGrpc.Shared.Server;

/// <summary>
/// Duplex streaming server method invoker.
/// </summary>
/// <typeparam name="TService">Service type for this method.</typeparam>
/// <typeparam name="TRequest">Request message type for this method.</typeparam>
/// <typeparam name="TResponse">Response message type for this method.</typeparam>
internal sealed class DuplexStreamingServerMethodInvoker<
#if NET5_0_OR_GREATER
    [DynamicallyAccessedMembers(RabbitGrpcProtocolConstants.ServiceAccessibility)]
#endif
    TService, TRequest, TResponse> : ServerMethodInvokerBase<TService, TRequest, TResponse>
    where TRequest : class
    where TResponse : class
    where TService : class
{
    private readonly DuplexStreamingServerMethod<TService, TRequest, TResponse> _invoker;
    private readonly DuplexStreamingServerMethod<TRequest, TResponse>? _pipelineInvoker;

    /// <summary>
    /// Creates a new instance of <see cref="DuplexStreamingServerMethodInvoker{TService, TRequest, TResponse}"/>.
    /// </summary>
    /// <param name="invoker">The duplex streaming method to invoke.</param>
    /// <param name="method">The description of the gRPC method.</param>
    /// <param name="options">The options used to execute the method.</param>
    /// <param name="serviceActivator">The service activator used to create service instances.</param>
    public DuplexStreamingServerMethodInvoker(
        DuplexStreamingServerMethod<TService, TRequest, TResponse> invoker,
        Method<TRequest, TResponse> method,
        MethodOptions options,
        IGrpcServiceActivator<TService> serviceActivator)
        : base(method, options, serviceActivator)
    {
        _invoker = invoker;

        if (Options.HasInterceptors)
        {
            var interceptorPipeline = new InterceptorPipelineBuilder<TRequest, TResponse>(Options.Interceptors);
            _pipelineInvoker = interceptorPipeline.DuplexStreamingPipeline(ResolvedInterceptorInvoker);
        }
    }

    private async Task ResolvedInterceptorInvoker(IAsyncStreamReader<TRequest> requestStream, IServerStreamWriter<TResponse> responseStream, ServerCallContext resolvedContext)
    {
        GrpcActivatorHandle<TService> serviceHandle = default;
        try
        {
            serviceHandle = ServiceActivator.Create(resolvedContext.GetHttpContext().RequestServices);
            await _invoker(
                serviceHandle.Instance,
                requestStream,
                responseStream,
                resolvedContext);
        }
        finally
        {
            if (serviceHandle.Instance != null)
            {
                await ServiceActivator.ReleaseAsync(serviceHandle);
            }
        }
    }
}