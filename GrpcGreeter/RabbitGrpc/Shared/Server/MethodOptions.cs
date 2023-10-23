using System.IO.Compression;
using Grpc.AspNetCore.Server;
using Grpc.Net.Compression;
using GrpcGreeter.RabbitGrpc.Server.Internal;
using InterceptorRegistration = GrpcGreeter.RabbitGrpc.Server.InterceptorRegistration;

namespace GrpcGreeter.RabbitGrpc.Shared.Server;

/// <summary>
/// Options used to execute a gRPC method.
/// </summary>
internal sealed class MethodOptions
{
    /// <summary>
    /// Gets the list of compression providers used to compress and decompress gRPC messages.
    /// </summary>
    public IReadOnlyDictionary<string, ICompressionProvider> CompressionProviders { get; }

    /// <summary>
    /// Get a collection of interceptors to be executed with every call. Interceptors are executed in order.
    /// </summary>
    public IReadOnlyList<InterceptorRegistration> Interceptors { get; }

    /// <summary>
    /// Gets the maximum message size in bytes that can be sent from the server.
    /// </summary>
    public int? MaxSendMessageSize { get; }

    /// <summary>
    /// Gets the maximum message size in bytes that can be received by the server.
    /// </summary>
    public int? MaxReceiveMessageSize { get; }

    /// <summary>
    /// Gets a value indicating whether detailed error messages are sent to the peer.
    /// Detailed error messages include details from exceptions thrown on the server.
    /// </summary>
    public bool? EnableDetailedErrors { get; }

    /// <summary>
    /// Gets the compression algorithm used to compress messages sent from the server.
    /// The request grpc-accept-encoding header value must contain this algorithm for it to
    /// be used.
    /// </summary>
    public string? ResponseCompressionAlgorithm { get; }

    /// <summary>
    /// Gets the compression level used to compress messages sent from the server.
    /// The compression level will be passed to the compression provider.
    /// </summary>
    public CompressionLevel? ResponseCompressionLevel { get; }

    // Fast check for whether the service has any interceptors
    internal bool HasInterceptors { get; }

    private MethodOptions(
        Dictionary<string, ICompressionProvider> compressionProviders,
        IReadOnlyList<InterceptorRegistration> interceptors,
        int? maxSendMessageSize,
        int? maxReceiveMessageSize,
        bool? enableDetailedErrors,
        string? responseCompressionAlgorithm,
        CompressionLevel? responseCompressionLevel)
    {
        CompressionProviders = compressionProviders;
        Interceptors = interceptors;
        HasInterceptors = interceptors.Count > 0;
        MaxSendMessageSize = maxSendMessageSize;
        MaxReceiveMessageSize = maxReceiveMessageSize;
        EnableDetailedErrors = enableDetailedErrors;
        ResponseCompressionAlgorithm = responseCompressionAlgorithm;
        ResponseCompressionLevel = responseCompressionLevel;

        if (ResponseCompressionAlgorithm != null)
        {
            if (!CompressionProviders.TryGetValue(ResponseCompressionAlgorithm, out var _))
            {
                throw new InvalidOperationException(
                    $"The configured response compression algorithm '{ResponseCompressionAlgorithm}' does not have a matching compression provider.");
            }
        }
    }

    public static MethodOptions Create(IEnumerable<GrpcServiceOptions> serviceOptions)
    {
        var resolvedCompressionProviders = new Dictionary<string, ICompressionProvider>(StringComparer.Ordinal);
        var tempInterceptors = new List<InterceptorRegistration>();
        int? maxSendMessageSize = null;
        var maxSendMessageSizeConfigured = false;
        int? maxReceiveMessageSize = RabbitGrpcServiceOptionsSetup.DefaultReceiveMaxMessageSize;
        var maxReceiveMessageSizeConfigured = false;
        bool? enableDetailedErrors = null;
        string? responseCompressionAlgorithm = null;
        CompressionLevel? responseCompressionLevel = null;

        var interceptors = new List<InterceptorRegistration>();

        return new MethodOptions
        (
            compressionProviders: resolvedCompressionProviders,
            interceptors: interceptors,
            maxSendMessageSize: maxSendMessageSize,
            maxReceiveMessageSize: maxReceiveMessageSize,
            enableDetailedErrors: enableDetailedErrors,
            responseCompressionAlgorithm: responseCompressionAlgorithm,
            responseCompressionLevel: responseCompressionLevel
        );
    }
}