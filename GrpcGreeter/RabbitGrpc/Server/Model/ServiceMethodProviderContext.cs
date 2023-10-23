using System.Diagnostics.CodeAnalysis;
using Grpc.AspNetCore.Server.Model;
using Grpc.Core;
using GrpcGreeter.RabbitGrpc.Server.Internal;
using GrpcGreeter.RabbitGrpc.Server.Model.Internal;
using Microsoft.AspNetCore.Routing.Patterns;

namespace GrpcGreeter.RabbitGrpc.Server.Model;

internal class ServiceMethodProviderContext<
#if NET5_0_OR_GREATER
    [DynamicallyAccessedMembers(RabbitGrpcProtocolConstants.ServiceAccessibility)]
#endif
    TService> where TService : class
{
    private readonly ServerCallHandlerFactory<TService> _serverCallHandlerFactory;

    public ServiceMethodProviderContext(ServerCallHandlerFactory<TService> serverCallHandlerFactory)
    {
        Methods = new List<MethodModel>();
        _serverCallHandlerFactory = serverCallHandlerFactory;
    }

    internal List<MethodModel> Methods { get; set; }

    public void AddUnaryMethod<TRequest, TResponse>(Method<TRequest, TResponse> method, List<object> metadata,
        UnaryServerMethod<TService, TRequest, TResponse> invoker) where TRequest : class where TResponse : class
    {
        var callHandler = _serverCallHandlerFactory.CreateUnary<TRequest, TResponse>(method, invoker);
        AddMethod(method, RoutePatternFactory.Parse(method.FullName), metadata, callHandler.HandleCallAsync);
    }

    public void AddServerStreamingMethod<TRequest, TResponse>(Method<TRequest, TResponse> method, List<object> metadata,
        ServerStreamingServerMethod<TService, TRequest, TResponse> invoker) where TRequest : class where TResponse : class
    {
        var callHandler = _serverCallHandlerFactory.CreateServerStreaming<TRequest, TResponse>(method, invoker);
        AddMethod(method, RoutePatternFactory.Parse(method.FullName), metadata, callHandler.HandleCallAsync);
    }

    public void AddClientStreamingMethod<TRequest, TResponse>(Method<TRequest, TResponse> method, List<object> metadata,
        ClientStreamingServerMethod<TService, TRequest, TResponse> invoker) where TRequest : class where TResponse : class
    {
        var callHandler = _serverCallHandlerFactory.CreateClientStreaming<TRequest, TResponse>(method, invoker);
        AddMethod(method, RoutePatternFactory.Parse(method.FullName), metadata, callHandler.HandleCallAsync);
    }

    public void AddDuplexStreamingMethod<TRequest, TResponse>(Method<TRequest, TResponse> method, List<object> metadata,
        DuplexStreamingServerMethod<TService, TRequest, TResponse> invoker) where TRequest : class where TResponse : class
    {
        var callHandler = _serverCallHandlerFactory.CreateDuplexStreaming<TRequest, TResponse>(method, invoker);
        AddMethod(method, RoutePatternFactory.Parse(method.FullName), metadata, callHandler.HandleCallAsync);
    }

    public void AddMethod<TRequest, TResponse>(Method<TRequest, TResponse> method, RoutePattern pattern, IList<object> metadata, MessageDelegate invoker)
        where TRequest : class
        where TResponse : class
    {
        var methodModel = new MethodModel(method, pattern, metadata, invoker);
        Methods.Add(methodModel);
    }
}