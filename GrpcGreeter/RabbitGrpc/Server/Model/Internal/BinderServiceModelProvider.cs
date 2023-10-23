using System.Diagnostics.CodeAnalysis;
using GrpcGreeter.RabbitGrpc.Server.Internal;
using GrpcGreeter.RabbitGrpc.Shared.Server;
using Log = GrpcGreeter.RabbitGrpc.Server.Model.Internal.BinderServiceMethodProviderLog;

namespace GrpcGreeter.RabbitGrpc.Server.Model.Internal;

internal class BinderServiceMethodProvider<
#if NET5_0_OR_GREATER
    [DynamicallyAccessedMembers(RabbitGrpcProtocolConstants.ServiceAccessibility)]
#endif
    TService> : IServiceMethodProvider<TService> where TService : class
{
    private readonly ILogger<BinderServiceMethodProvider<TService>> _logger;

    public BinderServiceMethodProvider(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<BinderServiceMethodProvider<TService>>();
    }

    public void OnServiceMethodDiscovery(ServiceMethodProviderContext<TService> context)
    {
        var bindMethodInfo = BindMethodFinder.GetBindMethod(typeof(TService));

        // Invoke BindService(ServiceBinderBase, BaseType)
        if (bindMethodInfo != null)
        {
            // The second parameter is always the service base type
            var serviceParameter = bindMethodInfo.GetParameters()[1];

            var binder = new ProviderServiceBinder<TService>(context, serviceParameter.ParameterType);

            try
            {
                bindMethodInfo.Invoke(null, new object?[] { binder, null });
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error binding gRPC service '{typeof(TService).Name}'.", ex);
            }
        }
        else
        {
            Log.BindMethodNotFound(_logger, typeof(TService));
        }
    }
}

internal static class BinderServiceMethodProviderLog
{
    private static readonly Action<ILogger, Type, Exception?> _bindMethodNotFound =
        LoggerMessage.Define<Type>(LogLevel.Debug, new EventId(1, "BindMethodNotFound"), "Could not find bind method for {ServiceType}.");

    public static void BindMethodNotFound(ILogger logger, Type serviceType)
    {
        _bindMethodNotFound(logger, serviceType, null);
    }
}
