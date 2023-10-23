using Grpc.Core;

namespace RabbitMqGreeterClient.Client.Internal;

internal interface IRabbitGrpcCall<TRequest, TResponse> : IDisposable, IMethod
    where TRequest : class
    where TResponse : class
{
    Task<TResponse> GetResponseAsync();
    Task<Metadata> GetResponseHeadersAsync();
    Status GetStatus();
    Metadata GetTrailers();

    void StartUnary(TRequest request);
}