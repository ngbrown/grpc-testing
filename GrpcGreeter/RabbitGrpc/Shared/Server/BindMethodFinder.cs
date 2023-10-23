using System.Reflection;
using Grpc.Core;

namespace GrpcGreeter.RabbitGrpc.Shared.Server;

internal class BindMethodFinder
{
    private const BindingFlags BindMethodBindingFlags = BindingFlags.Public | BindingFlags.Static;

    internal static MethodInfo? GetBindMethod(Type serviceType)
    {
        Type? currentServiceType = serviceType;
        BindServiceMethodAttribute? bindServiceMethod;
        do
        {
            // Search through base types for bind service attribute
            // We need to know the base service type because it is used with GetMethod below
            bindServiceMethod = currentServiceType.GetCustomAttribute<BindServiceMethodAttribute>();
            if (bindServiceMethod != null)
            {
                // Bind method will be public and static
                // Two parameters: ServiceBinderBase and the service type
                return bindServiceMethod.BindType.GetMethod(
                    bindServiceMethod.BindMethodName,
                    BindMethodBindingFlags,
                    binder: null,
                    new[] { typeof(ServiceBinderBase), currentServiceType },
                    Array.Empty<ParameterModifier>());
            }
        } while ((currentServiceType = currentServiceType.BaseType) != null);

        return null;
    }
}