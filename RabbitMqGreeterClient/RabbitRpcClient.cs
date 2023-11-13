using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace RabbitMqGreeterClient;

public class RabbitRpcClient : IDisposable
{
    private static readonly ushort PrefetchCount = 4 * 4;

    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly string _rpcQueueName;
    private readonly string _replyQueueName;
    private readonly string _userName;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<byte[]>> _callbackMapper = new();
    private bool _disposeConnection;

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(100);

    private RabbitRpcClient(IConnection connection, IModel channel, string rpcQueueName, string replyQueueName, string userName)
    {
        _connection = connection;
        _channel = channel;
        _rpcQueueName = rpcQueueName;
        _replyQueueName = replyQueueName;
        _userName = userName;
    }
    
    public static RabbitRpcClient Connect(IConnectionFactory factory, string rpcQueueName)
    {
        IConnection? connection = null;

        try
        {
            connection = factory.CreateConnection();
            var rpcClient = Connect(connection, rpcQueueName, factory.UserName);
            rpcClient._disposeConnection = true;
            return rpcClient;
        }
        catch (Exception)
        {
            connection?.Dispose();
            throw;
        }
    }

    public static RabbitRpcClient Connect(IConnection connection, string rpcQueueName, string userName)
    {
        IModel? channel = null;
        RabbitRpcClient rpcClient;

        try
        {
            channel = connection.CreateModel();
            channel.BasicQos(0, PrefetchCount, false);
            // declare a server-named queue
            var replyQueueName = channel.QueueDeclare(durable: false, exclusive: true, autoDelete: true).QueueName;

            rpcClient = new RabbitRpcClient(connection, channel, rpcQueueName, replyQueueName, userName);
        }
        catch (Exception)
        {
            channel?.Dispose();
            throw;
        }

        try
        {
            var consumer = new EventingBasicConsumer(rpcClient._channel);
            consumer.Received += rpcClient.OnMessageReceived;

            rpcClient._channel.BasicConsume(consumer: consumer,
                queue: rpcClient._replyQueueName,
                autoAck: true);
        }
        catch (Exception)
        {
            rpcClient.Dispose();
            throw;
        }

        return rpcClient;
    }

    private void OnMessageReceived(object? model, BasicDeliverEventArgs ea)
    {
        if (!_callbackMapper.TryRemove(ea.BasicProperties.CorrelationId, out var tcs)) return;

        var body = ea.Body.ToArray();
        tcs.TrySetResult(body);
    }

    public async Task<string> CallAsync(string message, CancellationToken cancellationToken = default)
    {
        var messageBytes = Encoding.UTF8.GetBytes(message);
        var body = await CallAsync(messageBytes, cancellationToken);
        var response = Encoding.UTF8.GetString(body);
        return response;
    }

    public async Task<byte[]> CallAsync(ReadOnlyMemory<byte> messageBytes, CancellationToken cancellationToken = default)
    {
        var correlationId = Guid.NewGuid().ToString();
        var props = CreateRequestProperties(correlationId);
        var tcs = new TaskCompletionSource<byte[]>();
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

    private IBasicProperties CreateRequestProperties(string correlationId)
    {
        IBasicProperties props = _channel.CreateBasicProperties();
        props.CorrelationId = correlationId;
        props.ReplyTo = _replyQueueName;
        props.Expiration = ((long)Math.Ceiling(this.Timeout.TotalMilliseconds)).ToString(CultureInfo.InvariantCulture);
        props.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        props.UserId = _userName;
        return props;
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
                    ex = toThrow = new TaskCanceledException(oce.Message, oce, cancellationToken);
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

    public void Dispose()
    {
        _channel.Dispose();

        if (_disposeConnection) _connection.Dispose();
    }
}