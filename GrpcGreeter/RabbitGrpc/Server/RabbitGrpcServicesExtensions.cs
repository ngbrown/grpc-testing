using Grpc.AspNetCore.Server;
using GrpcGreeter.RabbitGrpc.Server.Internal;
using GrpcGreeter.RabbitGrpc.Server.Model;
using GrpcGreeter.RabbitGrpc.Server.Model.Internal;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GrpcGreeter.RabbitGrpc.Server;

public static class RabbitGrpcServicesExtensions
{
    public static IGrpcServerBuilder AddRabbitGrpc(this IServiceCollection services)
    {
        services.TryAddSingleton(typeof(ServerCallHandlerFactory<>));

        services.TryAddSingleton<ServiceMethodsRegistry>();
        services.TryAddSingleton(typeof(ServiceRouteBuilder<>));
        services.TryAddEnumerable(ServiceDescriptor.Singleton(typeof(IServiceMethodProvider<>), typeof(BinderServiceMethodProvider<>)));

        services.AddHostedService<RabbitMqServer>();

        return new RabbitGrpcServerBuilder(services);
    }
}