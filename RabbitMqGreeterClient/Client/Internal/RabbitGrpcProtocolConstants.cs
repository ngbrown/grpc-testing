using Grpc.Core;

namespace RabbitMqGreeterClient.Client.Internal;

internal static class RabbitGrpcProtocolConstants
{
    internal static Status CreateClientCanceledStatus(Exception? exception) => new Status(StatusCode.Cancelled, "Call canceled by the client.", exception);

}