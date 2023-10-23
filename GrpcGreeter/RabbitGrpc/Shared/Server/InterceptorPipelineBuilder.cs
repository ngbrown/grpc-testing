using Grpc.AspNetCore.Server;
using Grpc.Core;
using Grpc.Core.Interceptors;
using IGrpcInterceptorActivator = GrpcGreeter.RabbitGrpc.Server.IGrpcInterceptorActivator;
using InterceptorRegistration = GrpcGreeter.RabbitGrpc.Server.InterceptorRegistration;

namespace GrpcGreeter.RabbitGrpc.Shared.Server;

internal class InterceptorPipelineBuilder<TRequest, TResponse>
    where TRequest : class
    where TResponse : class
{
    private readonly IReadOnlyList<InterceptorRegistration> _interceptors;

    public InterceptorPipelineBuilder(IReadOnlyList<InterceptorRegistration> interceptors)
    {
        _interceptors = interceptors;
    }


    public ClientStreamingServerMethod<TRequest, TResponse> ClientStreamingPipeline(ClientStreamingServerMethod<TRequest, TResponse> innerInvoker)
    {
        return BuildPipeline(innerInvoker, BuildInvoker);

        static ClientStreamingServerMethod<TRequest, TResponse> BuildInvoker(InterceptorRegistration interceptorRegistration, ClientStreamingServerMethod<TRequest, TResponse> next)
        {
            return async (requestStream, context) =>
            {
                var serviceProvider = context.GetHttpContext().RequestServices;
                var interceptorActivator = interceptorRegistration.GetActivator(serviceProvider);
                var interceptorHandle = CreateInterceptor(interceptorRegistration, interceptorActivator, serviceProvider);

                try
                {
                    return await interceptorHandle.Instance.ClientStreamingServerHandler(requestStream, context, next);
                }
                finally
                {
                    await interceptorActivator.ReleaseAsync(interceptorHandle);
                }
            };
        }
    }

    internal DuplexStreamingServerMethod<TRequest, TResponse> DuplexStreamingPipeline(DuplexStreamingServerMethod<TRequest, TResponse> innerInvoker)
    {
        return BuildPipeline(innerInvoker, BuildInvoker);

        static DuplexStreamingServerMethod<TRequest, TResponse> BuildInvoker(InterceptorRegistration interceptorRegistration, DuplexStreamingServerMethod<TRequest, TResponse> next)
        {
            return async (requestStream, responseStream, context) =>
            {
                var serviceProvider = context.GetHttpContext().RequestServices;
                var interceptorActivator = interceptorRegistration.GetActivator(serviceProvider);
                var interceptorHandle = CreateInterceptor(interceptorRegistration, interceptorActivator, serviceProvider);

                try
                {
                    await interceptorHandle.Instance.DuplexStreamingServerHandler(requestStream, responseStream, context, next);
                }
                finally
                {
                    await interceptorActivator.ReleaseAsync(interceptorHandle);
                }
            };
        }
    }

    internal ServerStreamingServerMethod<TRequest, TResponse> ServerStreamingPipeline(ServerStreamingServerMethod<TRequest, TResponse> innerInvoker)
    {
        return BuildPipeline(innerInvoker, BuildInvoker);

        static ServerStreamingServerMethod<TRequest, TResponse> BuildInvoker(InterceptorRegistration interceptorRegistration, ServerStreamingServerMethod<TRequest, TResponse> next)
        {
            return async (request, responseStream, context) =>
            {
                var serviceProvider = context.GetHttpContext().RequestServices;
                var interceptorActivator = interceptorRegistration.GetActivator(serviceProvider);
                var interceptorHandle = CreateInterceptor(interceptorRegistration, interceptorActivator, serviceProvider);

                if (interceptorHandle.Instance == null)
                {
                    throw new InvalidOperationException($"Could not construct Interceptor instance for type {interceptorRegistration.Type.FullName}");
                }

                try
                {
                    await interceptorHandle.Instance.ServerStreamingServerHandler(request, responseStream, context, next);
                }
                finally
                {
                    await interceptorActivator.ReleaseAsync(interceptorHandle);
                }
            };
        }
    }

    internal UnaryServerMethod<TRequest, TResponse> UnaryPipeline(UnaryServerMethod<TRequest, TResponse> innerInvoker)
    {
        return BuildPipeline(innerInvoker, BuildInvoker);

        static UnaryServerMethod<TRequest, TResponse> BuildInvoker(InterceptorRegistration interceptorRegistration, UnaryServerMethod<TRequest, TResponse> next)
        {
            return async (request, context) =>
            {
                var serviceProvider = context.GetHttpContext().RequestServices;
                var interceptorActivator = interceptorRegistration.GetActivator(serviceProvider);
                var interceptorHandle = CreateInterceptor(interceptorRegistration, interceptorActivator, serviceProvider);

                try
                {
                    return await interceptorHandle.Instance.UnaryServerHandler(request, context, next);
                }
                finally
                {
                    await interceptorActivator.ReleaseAsync(interceptorHandle);
                }
            };
        }
    }

    private T BuildPipeline<T>(T innerInvoker, Func<InterceptorRegistration, T, T> wrapInvoker)
    {
        // The inner invoker will create the service instance and invoke the method
        var resolvedInvoker = innerInvoker;

        // The list is reversed during construction so the first interceptor is built last and invoked first
        for (var i = _interceptors.Count - 1; i >= 0; i--)
        {
            resolvedInvoker = wrapInvoker(_interceptors[i], resolvedInvoker);
        }

        return resolvedInvoker;
    }

    private static GrpcActivatorHandle<Interceptor> CreateInterceptor(InterceptorRegistration interceptorRegistration, IGrpcInterceptorActivator interceptorActivator, IServiceProvider serviceProvider)
    {
        var interceptorHandle = interceptorActivator.Create(serviceProvider, interceptorRegistration);

        if (interceptorHandle.Instance == null)
        {
            throw new InvalidOperationException($"Could not construct Interceptor instance for type {interceptorRegistration.Type.FullName}");
        }

        return interceptorHandle;
    }
}