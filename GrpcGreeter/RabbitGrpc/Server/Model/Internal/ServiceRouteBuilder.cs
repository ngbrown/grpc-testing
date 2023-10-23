using System.Diagnostics.CodeAnalysis;
using Grpc.Core;
using GrpcGreeter.RabbitGrpc.Server.Internal;
using Log = GrpcGreeter.RabbitGrpc.Server.Model.Internal.ServiceRouteBuilderLog;

namespace GrpcGreeter.RabbitGrpc.Server.Model.Internal;

internal class ServiceRouteBuilder<
    [DynamicallyAccessedMembers(RabbitGrpcProtocolConstants.ServiceAccessibility)] TService> where TService : class
{
    private readonly IEnumerable<IServiceMethodProvider<TService>> _serviceMethodProviders;
    private readonly ServerCallHandlerFactory<TService> _serverCallHandlerFactory;
    private readonly ServiceMethodsRegistry _serviceMethodsRegistry;
    private readonly ILogger _logger;

    public ServiceRouteBuilder(
        IEnumerable<IServiceMethodProvider<TService>> serviceMethodProviders,
        ServerCallHandlerFactory<TService> serverCallHandlerFactory,
        ServiceMethodsRegistry serviceMethodsRegistry,
        ILoggerFactory loggerFactory)
    {
        _serviceMethodProviders = serviceMethodProviders.ToList();
        _serverCallHandlerFactory = serverCallHandlerFactory;
        _serviceMethodsRegistry = serviceMethodsRegistry;
        _logger = loggerFactory.CreateLogger<ServiceRouteBuilder<TService>>();
    }

    public void Build()
    {
        Log.DiscoveringServiceMethods(_logger, typeof(TService));

        var serviceMethodProviderContext = new ServiceMethodProviderContext<TService>(_serverCallHandlerFactory);
        foreach (var serviceMethodProvider in _serviceMethodProviders)
        {
            serviceMethodProvider.OnServiceMethodDiscovery(serviceMethodProviderContext);
        }

        if (serviceMethodProviderContext.Methods.Count > 0)
        {
            foreach (var method in serviceMethodProviderContext.Methods)
            {
                Log.AddedServiceMethod(
                    _logger,
                    method.Method.Name,
                    method.Method.ServiceName,
                    method.Method.Type,
                    method.Pattern.RawText ?? string.Empty);
            }
        }
        else
        {
            Log.NoServiceMethodsDiscovered(_logger, typeof(TService));
        }

        _serviceMethodsRegistry.Methods.AddRange(serviceMethodProviderContext.Methods);
    }
}

internal static class ServiceRouteBuilderLog
{
    private static readonly Action<ILogger, string, string, MethodType, string, Exception?> _addedServiceMethod =
        LoggerMessage.Define<string, string, MethodType, string>(LogLevel.Trace, new EventId(1, "AddedServiceMethod"), "Added RabbitRPC method '{MethodName}' to service '{ServiceName}'. Method type: {MethodType}, route pattern: '{RoutePattern}'.");

    private static readonly Action<ILogger, Type, Exception?> _discoveringServiceMethods =
        LoggerMessage.Define<Type>(LogLevel.Trace, new EventId(2, "DiscoveringServiceMethods"), "Discovering RabbitRPC methods for {ServiceType}.");

    private static readonly Action<ILogger, Type, Exception?> _noServiceMethodsDiscovered =
        LoggerMessage.Define<Type>(LogLevel.Debug, new EventId(3, "NoServiceMethodsDiscovered"), "No RabbitRPC methods discovered for {ServiceType}.");

    public static void AddedServiceMethod(ILogger logger, string methodName, string serviceName, MethodType methodType, string routePattern)
    {
        if (logger.IsEnabled(LogLevel.Trace))
        {
            _addedServiceMethod(logger, methodName, serviceName, methodType, routePattern, null);
        }
    }

    public static void DiscoveringServiceMethods(ILogger logger, Type serviceType)
    {
        _discoveringServiceMethods(logger, serviceType, null);
    }

    public static void NoServiceMethodsDiscovered(ILogger logger, Type serviceType)
    {
        _noServiceMethodsDiscovered(logger, serviceType, null);
    }
}

internal class ServiceMethodsRegistry
{
    public List<MethodModel> Methods { get; } = new();
}