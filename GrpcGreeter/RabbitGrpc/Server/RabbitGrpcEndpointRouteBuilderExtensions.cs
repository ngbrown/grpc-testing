using System.Diagnostics.CodeAnalysis;
using GrpcGreeter.RabbitGrpc.Server.Internal;
using GrpcGreeter.RabbitGrpc.Server.Model.Internal;

namespace GrpcGreeter.RabbitGrpc.Server;

public static class RabbitGrpcEndpointRouteBuilderExtensions
{
    public static void RegisterRabbitRpcService<
        [DynamicallyAccessedMembers(RabbitGrpcProtocolConstants.ServiceAccessibility)] TService>(
        this IEndpointRouteBuilder builder) where TService : class
    {
        ArgumentNullException.ThrowIfNull(builder);

        // TODO: ValidateServicesRegistered(builder.ServiceProvider);

        var serviceRouteBuilder = builder.ServiceProvider.GetRequiredService<ServiceRouteBuilder<TService>>();
        serviceRouteBuilder.Build();
    }
}