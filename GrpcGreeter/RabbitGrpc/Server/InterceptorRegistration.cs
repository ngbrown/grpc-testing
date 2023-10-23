using System.Diagnostics.CodeAnalysis;
using Grpc.AspNetCore.Server;
using Grpc.Core.Interceptors;

namespace GrpcGreeter.RabbitGrpc.Server;

/// <summary>
/// Representation of a registration of an <see cref="Interceptor"/> in the server pipeline.
/// </summary>
internal class InterceptorRegistration
{
#if NET5_0_OR_GREATER
    internal const DynamicallyAccessedMemberTypes InterceptorAccessibility = DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods;
#endif

    internal object[] _args;

    internal InterceptorRegistration(
#if NET5_0_OR_GREATER
        [DynamicallyAccessedMembers(InterceptorAccessibility)]
#endif
        Type type, object[] arguments)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(arguments);

        for (var i = 0; i < arguments.Length; i++)
        {
            if (arguments[i] == null)
            {
                throw new ArgumentException("Interceptor arguments contains a null value. Null interceptor arguments are not supported.", nameof(arguments));
            }
        }

        Type = type;
        _args = arguments;
    }

    /// <summary>
    /// Get the type of the interceptor.
    /// </summary>
#if NET5_0_OR_GREATER
    [DynamicallyAccessedMembers(InterceptorAccessibility)]
#endif
    public Type Type { get; }

    /// <summary>
    /// Get the arguments used to create the interceptor.
    /// </summary>
    public IReadOnlyList<object> Arguments => _args;

    private IGrpcInterceptorActivator? _interceptorActivator;
    private ObjectFactory? _factory;

#if NET5_0_OR_GREATER
    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026:UnrecognizedReflectionPattern",
        Justification = "Type parameter members are preserved with DynamicallyAccessedMembers on InterceptorRegistration.Type property.")]
    [UnconditionalSuppressMessage("AotAnalysis", "IL3050:RequiresDynamicCode",
        Justification = "Type definition is explicitly specified and type argument is always an Interceptor type.")]
#endif
    internal IGrpcInterceptorActivator GetActivator(IServiceProvider serviceProvider)
    {
        // Not thread safe. Side effect is resolving the service twice.
        if (_interceptorActivator == null)
        {
            _interceptorActivator = (IGrpcInterceptorActivator)serviceProvider.GetRequiredService(typeof(IGrpcInterceptorActivator<>).MakeGenericType(Type));
        }

        return _interceptorActivator;
    }

    internal ObjectFactory GetFactory()
    {
        // Not thread safe. Side effect is resolving the factory twice.
        if (_factory == null)
        {
            _factory = ActivatorUtilities.CreateFactory(Type, _args.Select(a => a.GetType()).ToArray());
        }

        return _factory;
    }
}