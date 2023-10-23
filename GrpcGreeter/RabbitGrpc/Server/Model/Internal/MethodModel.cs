using Grpc.Core;
using Microsoft.AspNetCore.Routing.Patterns;

namespace GrpcGreeter.RabbitGrpc.Server.Model.Internal;

internal class MethodModel
{
    public MethodModel(IMethod method, RoutePattern pattern, IList<object> metadata, MessageDelegate requestDelegate)
    {
        Method = method;
        Pattern = pattern;
        Metadata = metadata;
        RequestDelegate = requestDelegate;
    }

    public IMethod Method { get; }
    public RoutePattern Pattern { get; }
    public IList<object> Metadata { get; }
    public MessageDelegate RequestDelegate { get; }
}