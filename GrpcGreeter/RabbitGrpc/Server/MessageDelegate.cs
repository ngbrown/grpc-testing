namespace GrpcGreeter.RabbitGrpc.Server;

/// <summary>
/// A function that can process an Rabbit RPC request.
/// </summary>
/// <param name="context">The <see cref="RpcContext"/> for the request.</param>
/// <returns>A task that represents the completion of request processing.</returns>
public delegate Task MessageDelegate(RpcContext context);
