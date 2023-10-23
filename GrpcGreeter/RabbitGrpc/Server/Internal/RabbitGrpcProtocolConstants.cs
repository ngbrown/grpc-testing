using System.Diagnostics.CodeAnalysis;

namespace GrpcGreeter.RabbitGrpc.Server.Internal;

internal static class RabbitGrpcProtocolConstants
{
    internal const string ResponseContentType = "application/x-protobuf; messageType=\"rabbit.rpc.RabbitRpcResponse\"";
    internal const string RequestContentType = "application/x-protobuf; messageType=\"rabbit.rpc.RabbitRpcRequest\"";

#if NET5_0_OR_GREATER
    internal const DynamicallyAccessedMemberTypes ServiceAccessibility = DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods;
#endif
}