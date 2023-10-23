using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace RabbitMqGreeterClient.Client.Shared;

internal static class CompatibilityHelpers
{
    [Conditional("DEBUG")]
    public static void Assert([DoesNotReturnIf(false)] bool condition, string? message = null)
    {
        Debug.Assert(condition, message);
    }
    
    public static bool IsCompletedSuccessfully(this Task task)
    {
        // IsCompletedSuccessfully is the faster method, but only currently exposed on .NET Core 2.0+
#if !NETSTANDARD2_0 && !NET462
        return task.IsCompletedSuccessfully;
#else
        return task.Status == TaskStatus.RanToCompletion;
#endif
    }

#if !NETSTANDARD2_0 && !NET462
    public static bool IsCompletedSuccessfully(this ValueTask task)
    {
        return task.IsCompletedSuccessfully;
    }

    public static bool IsCompletedSuccessfully<T>(this ValueTask<T> task)
    {
        return task.IsCompletedSuccessfully;
    }
#endif

    public static CancellationTokenRegistration RegisterWithCancellationTokenCallback(CancellationToken cancellationToken, Action<object?, CancellationToken> callback, object? state)
    {
        // Register overload that provides the CT to the callback required .NET 6 or greater.
        // Fallback to creating a closure in older platforms.
#if NET6_0_OR_GREATER
        return cancellationToken.Register(callback, state);
#else
        return cancellationToken.Register((state) => callback(state, cancellationToken), state);
#endif
    }
}