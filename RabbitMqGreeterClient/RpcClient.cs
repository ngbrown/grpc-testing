using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace RabbitMqGreeterClient;

public class RpcClient : IDisposable
{
    private const string QUEUE_NAME = "rpc_queue";

    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly string _replyQueueName;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _callbackMapper = new();

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(100);

    private RpcClient(IConnection connection, IModel channel, string replyQueueName)
    {
        _connection = connection;
        _channel = channel;
        _replyQueueName = replyQueueName;
    }

    public static RpcClient Connect()
    {
        var factory = new ConnectionFactory { HostName = "localhost", UserName = "guest", Password = "guest"  };

        IConnection? connection = null;
        IModel? channel = null;
        RpcClient rpcClient;

        try
        {
            connection = factory.CreateConnection();
            channel = connection.CreateModel();
            // declare a server-named queue
            var replyQueueName = channel.QueueDeclare().QueueName;

            rpcClient = new RpcClient(connection, channel, replyQueueName);
        }
        catch (Exception)
        {
            channel?.Dispose();
            connection?.Dispose();
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
        var response = Encoding.UTF8.GetString(body);
        tcs.TrySetResult(response);
    }

    public Task<string> CallAsync(string message, CancellationToken cancellationToken = default)
    {
        var messageBytes = Encoding.UTF8.GetBytes(message);
        return CallAsync(messageBytes, cancellationToken);
    }

    public Task<string> CallAsync(ReadOnlyMemory<byte> messageBytes, CancellationToken cancellationToken = default)
    {
        IBasicProperties props = _channel.CreateBasicProperties();
        var correlationId = Guid.NewGuid().ToString();
        props.CorrelationId = correlationId;
        props.ReplyTo = _replyQueueName;
        props.Expiration = ((long)Math.Ceiling(this.Timeout.TotalMilliseconds)).ToString(CultureInfo.InvariantCulture);
        var tcs = new TaskCompletionSource<string>();
        _callbackMapper.TryAdd(correlationId, tcs);

        _channel.BasicPublish(exchange: string.Empty,
            routingKey: QUEUE_NAME,
            basicProperties: props,
            body: messageBytes);

        var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(this.Timeout);
        var timeoutToken = timeoutCts.Token;

        timeoutToken.Register(() =>
        {
            _callbackMapper.TryRemove(correlationId, out var tcs2);
            tcs2?.TrySetCanceled(timeoutToken);
        });
        return tcs.Task;
    }

    public void Dispose()
    {
        _channel.Dispose();
        _connection.Dispose();
    }
}