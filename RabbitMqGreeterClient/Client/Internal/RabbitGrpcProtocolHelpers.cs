using Grpc.Core;

namespace RabbitMqGreeterClient.Client.Internal;

internal class RabbitGrpcProtocolHelpers
{
    internal static Status CreateStatusFromException(string summary, Exception ex, StatusCode? statusCode = null)
    {
        var exceptionMessage = ConvertToRpcExceptionMessage(ex);
        statusCode ??= ResolveRpcExceptionStatusCode(ex);

        return new Status(statusCode.Value, summary + " " + exceptionMessage, ex);
    }

    internal static StatusCode ResolveRpcExceptionStatusCode(Exception exception)
    {
        StatusCode? statusCode = null;

        // TODO
        return statusCode ?? StatusCode.Internal;
    }

    private static string ConvertToRpcExceptionMessage(Exception exception)
    {
        return exception.Message;
    }
}