using RabbitRpc.Core;

namespace GrpcGreeter.RabbitGrpc.Server;

public class RpcContext
{
    public RpcContext(RabbitRpcRequest rpcRequest, IServiceProvider requestServices, CancellationToken cancellationToken)
    {
        Request = rpcRequest;
        RequestServices = requestServices;
        RequestAborted = cancellationToken;
        Response = new RabbitRpcResponse();
    }

    public CancellationToken RequestAborted { get; set; }
    public RabbitRpcRequest Request { get; }
    public RabbitRpcResponse Response { get; }
    public IServiceProvider RequestServices { get; set; }
}