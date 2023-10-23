using System.Collections.Concurrent;
using System.Globalization;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMqGreeterClient.Client.Internal;
using RabbitRpc.Core;

namespace RabbitMqGreeterClient.Client;

internal class RabbitRpcChannel : Grpc.Core.ChannelBase, IDisposable
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly string _rpcQueueName;
    private readonly string _replyQueueName;
    private readonly string _userName;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<RabbitRpcResponse>> _callbackMapper = new();
    private bool _disposeConnection;
    
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(100);
    public bool Disposed { get; private set; }
    internal ILoggerFactory LoggerFactory { get; }
    internal ILogger Logger { get; }


    internal bool DisableClientDeadline;
    internal long MaxTimerDueTime = uint.MaxValue - 1; // Max System.Threading.Timer due time

    public RabbitRpcChannel(IConnection connection, IModel channel, string rpcQueueName, string replyQueueName, string userName) : base(connection.Endpoint.HostName)
    {
        _connection = connection;
        _channel = channel;
        _rpcQueueName = rpcQueueName;
        _replyQueueName = replyQueueName;
        _userName = userName;
        LoggerFactory = NullLoggerFactory.Instance;
        Logger = LoggerFactory.CreateLogger(typeof(RabbitRpcChannel));
    }

    public static RabbitRpcChannel ForQueue(IConnectionFactory factory, string rpcQueueName)
    {
        IConnection? connection = null;

        try
        {
            connection = factory.CreateConnection();
            var rpcClient = ForConnection(connection, rpcQueueName, factory.UserName);
            rpcClient._disposeConnection = true;
            return rpcClient;
        }
        catch (Exception)
        {
            connection?.Dispose();
            throw;
        }
    }

    public static RabbitRpcChannel ForConnection(IConnection connection, string rpcQueueName, string userName)
    {
        IModel? channel = null;
        RabbitRpcChannel rpcChannel;

        try
        {
            channel = connection.CreateModel();
            // declare a server-named queue
            var replyQueueName = channel.QueueDeclare(durable: false, exclusive: true, autoDelete: true).QueueName;

            rpcChannel = new RabbitRpcChannel(connection, channel, rpcQueueName, replyQueueName, userName);
        }
        catch (Exception)
        {
            channel?.Dispose();
            throw;
        }

        try
        {
            var consumer = new EventingBasicConsumer(rpcChannel._channel);
            consumer.Received += rpcChannel.OnMessageReceived;

            rpcChannel._channel.BasicConsume(consumer: consumer,
                queue: rpcChannel._replyQueueName,
                autoAck: true);
        }
        catch (Exception)
        {
            rpcChannel.Dispose();
            throw;
        }

        return rpcChannel;
    }

    private void OnMessageReceived(object? model, BasicDeliverEventArgs ea)
    {
        if (!_callbackMapper.TryRemove(ea.BasicProperties.CorrelationId, out var tcs)) return;

        var response = new RabbitRpcResponse();
        response.MergeFrom(ea.Body.Span);
        if (ea.BasicProperties.IsHeadersPresent())
        {
            foreach (var (key, value) in ea.BasicProperties.Headers)
            {
                if (key != null && value is string s)
                {
                    response.Headers.Add(new RabbitRpcHeader { Key = key, Value = s });
                }
            }
        }
        tcs.TrySetResult(response);
    }

    public override CallInvoker CreateCallInvoker()
    {
        return new RabbitRpcCallInvoker(this);
    }

    internal async Task<RabbitRpcResponse> CallAsync(ReadOnlyMemory<byte> messageBytes, CancellationToken cancellationToken = default)
    {
        IBasicProperties props = _channel.CreateBasicProperties();
        var correlationId = Guid.NewGuid().ToString();
        props.CorrelationId = correlationId;
        props.ReplyTo = _replyQueueName;
        props.Expiration = ((long)Math.Ceiling(this.Timeout.TotalMilliseconds)).ToString(CultureInfo.InvariantCulture);
        props.ContentType = "application/x-protobuf; messageType=\"rabbit.rpc.RabbitRpcRequest\"";
        props.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        props.UserId = _userName;
        var tcs = new TaskCompletionSource<RabbitRpcResponse>();
        _callbackMapper.TryAdd(correlationId, tcs);

        _channel.BasicPublish(exchange: string.Empty,
            routingKey: _rpcQueueName,
            basicProperties: props,
            body: messageBytes);

        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        if (this.Timeout != TimeSpan.MaxValue)
        {
            cts.CancelAfter(this.Timeout);
        }
        var ctsToken = cts.Token;

        ctsToken.Register(() =>
        {
            _callbackMapper.TryRemove(correlationId, out var tcs2);
            tcs2?.TrySetCanceled(ctsToken);
        });

        try
        {
            return await tcs.Task;
        }
        catch (Exception ex)
        {
            HandleFailure(ex, cts, cancellationToken);
            throw;
        }
        finally
        {
            cts.Dispose();
        }
    }

    /// <summary>
    /// In case of timeout, nest the TimeoutException within the TaskCanceledException.
    /// </summary>
    /// <remarks>
    /// See https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpclient.getasync?view=net-7.0
    /// </remarks>
    private void HandleFailure(Exception ex, CancellationTokenSource cts, CancellationToken cancellationToken)
    {
        Exception? toThrow = null;

        if (ex is OperationCanceledException oce)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                if (oce.CancellationToken != cancellationToken)
                {
                    // We got a cancellation exception, and the caller requested cancellation, but the exception doesn't contain that token.
                    // Massage things so that the cancellation exception we propagate appropriately contains the caller's token (it's possible
                    // multiple things caused cancellation, in which case we can attribute it to the caller's token, or it's possible the
                    // exception contains the linked token source, in which case that token isn't meaningful to the caller).
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER || NET5_0_OR_GREATER
                    ex = toThrow = new TaskCanceledException(oce.Message, oce, cancellationToken);
#else
                    ex = toThrow = new TaskCanceledException(oce.Message, oce);
#endif
                }
            }
            else if (cts.IsCancellationRequested)
            {
                // If the linked cancellation token source was canceled, but cancellation wasn't requested by the caller's token
                // the only other cause could be a timeout.  Treat it as such.

                // cancellationToken could have been triggered right after we checked it, but before we checked the cts.
                // We must check it again to avoid reporting a timeout when one did not occur.
                if (!cancellationToken.IsCancellationRequested)
                {
                    ex = toThrow = new TaskCanceledException(
                        $"RpcClient Request Timeout after {this.Timeout.TotalSeconds} seconds",
                        new TimeoutException(ex.Message, ex));
                }
            }
        }

        // TODO: Log failure

        if (toThrow != null)
        {
            throw toThrow;
        }
    }

    protected override Task ShutdownAsyncCore()
    {
        _channel.Close();
        if (_disposeConnection) _connection.Close();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _channel.Dispose();

        if (_disposeConnection) _connection.Dispose();

        Disposed = true;
    }
}
