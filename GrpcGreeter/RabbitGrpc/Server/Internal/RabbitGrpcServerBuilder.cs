using Grpc.AspNetCore.Server;

namespace GrpcGreeter.RabbitGrpc.Server.Internal;

internal class RabbitGrpcServerBuilder : IGrpcServerBuilder
{
    public IServiceCollection Services { get; }

    public RabbitGrpcServerBuilder(IServiceCollection services)
    {
        Services = services;
    }
}