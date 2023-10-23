using System.Diagnostics.CodeAnalysis;
using GrpcGreeter.RabbitGrpc.Server.Internal;

namespace GrpcGreeter.RabbitGrpc.Server.Model;

/// <summary>
/// Defines a contract for specifying methods for <typeparamref name="TService"/>.
/// </summary>
/// <remarks>
/// <para>
/// On application initialization, gRPC invokes all registered instances of <see cref="IServiceMethodProvider{TService}"/> to 
/// perform method discovery. 
/// <see cref="IServiceMethodProvider{TService}"/> instances are invoked in the order they are registered.
/// </para>
/// </remarks>
internal interface IServiceMethodProvider<
#if NET5_0_OR_GREATER
    [DynamicallyAccessedMembers(RabbitGrpcProtocolConstants.ServiceAccessibility)]
#endif
    TService> where TService : class
{
    /// <summary>
    /// Called to execute the provider.
    /// </summary>
    /// <param name="context">The <see cref="ServiceMethodProviderContext{TService}"/>.</param>
    void OnServiceMethodDiscovery(ServiceMethodProviderContext<TService> context);
}