using Grpc.AspNetCore.Server;
using Grpc.Core.Interceptors;

namespace GrpcGreeter.RabbitGrpc.Server;

/// <summary>
/// An interceptor activator abstraction.
/// </summary>
internal interface IGrpcInterceptorActivator
{
    /// <summary>
    /// Creates an interceptor.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="interceptorRegistration">The arguments to pass to the interceptor type instance's constructor.</param>
    /// <returns>The created interceptor.</returns>
    GrpcActivatorHandle<Interceptor> Create(IServiceProvider serviceProvider, InterceptorRegistration interceptorRegistration);

    /// <summary>
    /// Releases the specified interceptor.
    /// </summary>
    /// <param name="interceptor">The interceptor to release.</param>
    ValueTask ReleaseAsync(GrpcActivatorHandle<Interceptor> interceptor);
}