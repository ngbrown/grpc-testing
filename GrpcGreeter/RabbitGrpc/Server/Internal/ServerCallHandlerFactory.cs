using System.Diagnostics.CodeAnalysis;
using Grpc.AspNetCore.Server;
using Grpc.AspNetCore.Server.Model;
using Grpc.Core;
using GrpcGreeter.RabbitGrpc.Server.Internal.CallHandlers;
using GrpcGreeter.RabbitGrpc.Shared.Server;
using Microsoft.Extensions.Options;
using Log = GrpcGreeter.RabbitGrpc.Server.Internal.ServerCallHandlerFactoryLog;

namespace GrpcGreeter.RabbitGrpc.Server.Internal;

internal class ServerCallHandlerFactory<
#if NET5_0_OR_GREATER
    [DynamicallyAccessedMembers(RabbitGrpcProtocolConstants.ServiceAccessibility)]
#endif
    TService> where TService : class
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly IGrpcServiceActivator<TService> _serviceActivator;
    private readonly GrpcServiceOptions _globalOptions;
    private readonly GrpcServiceOptions<TService> _serviceOptions;

    public ServerCallHandlerFactory(
        ILoggerFactory loggerFactory,
        IOptions<GrpcServiceOptions> globalOptions,
        IOptions<GrpcServiceOptions<TService>> serviceOptions,
        IGrpcServiceActivator<TService> serviceActivator)
    {
        _loggerFactory = loggerFactory;
        _serviceActivator = serviceActivator;
        _serviceOptions = serviceOptions.Value;
        _globalOptions = globalOptions.Value;
    }

    // Internal for testing
    internal MethodOptions CreateMethodOptions()
    {
        return MethodOptions.Create(new[] { _globalOptions, _serviceOptions });
    }

    public UnaryServerCallHandler<TService, TRequest, TResponse> CreateUnary<TRequest, TResponse>(Method<TRequest, TResponse> method, UnaryServerMethod<TService, TRequest, TResponse> invoker)
        where TRequest : class
        where TResponse : class
    {
        var options = CreateMethodOptions();
        var methodInvoker = new UnaryServerMethodInvoker<TService, TRequest, TResponse>(invoker, method, options, _serviceActivator);

        return new UnaryServerCallHandler<TService, TRequest, TResponse>(methodInvoker, _loggerFactory);
    }

    public ClientStreamingServerCallHandler<TService, TRequest, TResponse> CreateClientStreaming<TRequest, TResponse>(Method<TRequest, TResponse> method, ClientStreamingServerMethod<TService, TRequest, TResponse> invoker)
        where TRequest : class
        where TResponse : class
    {
        var options = CreateMethodOptions();
        var methodInvoker = new ClientStreamingServerMethodInvoker<TService, TRequest, TResponse>(invoker, method, options, _serviceActivator);

        return new ClientStreamingServerCallHandler<TService, TRequest, TResponse>(methodInvoker, _loggerFactory);
    }

    public DuplexStreamingServerCallHandler<TService, TRequest, TResponse> CreateDuplexStreaming<TRequest, TResponse>(Method<TRequest, TResponse> method, DuplexStreamingServerMethod<TService, TRequest, TResponse> invoker)
        where TRequest : class
        where TResponse : class
    {
        var options = CreateMethodOptions();
        var methodInvoker = new DuplexStreamingServerMethodInvoker<TService, TRequest, TResponse>(invoker, method, options, _serviceActivator);

        return new DuplexStreamingServerCallHandler<TService, TRequest, TResponse>(methodInvoker, _loggerFactory);
    }

    public ServerStreamingServerCallHandler<TService, TRequest, TResponse> CreateServerStreaming<TRequest, TResponse>(Method<TRequest, TResponse> method, ServerStreamingServerMethod<TService, TRequest, TResponse> invoker)
        where TRequest : class
        where TResponse : class
    {
        var options = CreateMethodOptions();
        var methodInvoker = new ServerStreamingServerMethodInvoker<TService, TRequest, TResponse>(invoker, method, options, _serviceActivator);

        return new ServerStreamingServerCallHandler<TService, TRequest, TResponse>(methodInvoker, _loggerFactory);
    }

    public MessageDelegate CreateUnimplementedMethod()
    {
        var logger = _loggerFactory.CreateLogger<ServerCallHandlerFactory<TService>>();

        return context =>
        {
            var unimplementedService = "<unknown>"; // context.Request.RouteValues["unimplementedMethod"]?.ToString() ?? "<unknown>";
            Log.ServiceUnimplemented(logger, unimplementedService);
            return Task.CompletedTask;
        };
    }

    public bool IgnoreUnknownServices => false; // _globalOptions.IgnoreUnknownServices ?? false;
    public bool IgnoreUnknownMethods => false; // _serviceOptions.IgnoreUnknownServices ?? _globalOptions.IgnoreUnknownServices ?? false;

    public MessageDelegate CreateUnimplementedService()
    {
        var logger = _loggerFactory.CreateLogger<ServerCallHandlerFactory<TService>>();

        return context =>
        {
            var unimplementedService = "<unknown>"; // context.Request.RouteValues["unimplementedService"]?.ToString() ?? "<unknown>";
            Log.ServiceUnimplemented(logger, unimplementedService);
            return Task.CompletedTask;
        };
    }
}

internal static class ServerCallHandlerFactoryLog
{
    private static readonly Action<ILogger, string, Exception?> _serviceUnimplemented =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(1, "ServiceUnimplemented"), "Service '{ServiceName}' is unimplemented.");

    private static readonly Action<ILogger, string, Exception?> _methodUnimplemented =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(2, "MethodUnimplemented"), "Method '{MethodName}' is unimplemented.");

    public static void ServiceUnimplemented(ILogger logger, string serviceName)
    {
        _serviceUnimplemented(logger, serviceName, null);
    }

    public static void MethodUnimplemented(ILogger logger, string methodName)
    {
        _methodUnimplemented(logger, methodName, null);
    }
}