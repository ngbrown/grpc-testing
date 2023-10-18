using System.Globalization;
using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace GrpcGreeter;

public class RabbitMqServer : IHostedService
{
    private const string QUEUE_NAME = "rpc_queue";

    private readonly ILogger<RabbitMqServer> _logger;

    private IConnection? _connection;
    private readonly List<IModel> _channels = new();
    private ushort _parallelCount = 4;
    private readonly CancellationTokenSource _serviceCancellationTokenSource = new();

    public RabbitMqServer(ILogger<RabbitMqServer> logger)
    {
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        var factory = new ConnectionFactory { HostName = "localhost", UserName = "guest", Password = "guest" };
        _connection = factory.CreateConnection();

        for (int i = 0; i < _parallelCount; i++)
        {
            var channel = _connection.CreateModel();
            this._channels.Add(channel);

            if (i == 0)
            {
                channel.QueueDeclare(queue: QUEUE_NAME,
                    durable: false,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null);
            }

            channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

            var consumer = new EventingBasicConsumer(channel);
            consumer.Received += OnMessageReceived;
            channel.BasicConsume(queue: QUEUE_NAME,
                autoAck: false,
                consumer: consumer);
        }

        this._logger.LogInformation("Awaiting RPC requests");

        return Task.CompletedTask;
    }

    private void OnMessageReceived(object? consumer, BasicDeliverEventArgs ea)
    {
        var channel = (consumer as EventingBasicConsumer)?.Model;
        if (channel == null || channel.IsClosed) throw new OperationCanceledException();

        string response = string.Empty;

        var body = ea.Body.ToArray();
        var props = ea.BasicProperties;
        var replyProps = channel.CreateBasicProperties();
        replyProps.CorrelationId = props.CorrelationId;

        CancellationToken cancellationToken = this._serviceCancellationTokenSource.Token;
        CancellationTokenSource? cts = default;
        if (!string.IsNullOrWhiteSpace(props.Expiration) && int.TryParse(props.Expiration, out int expirationMs))
        {
            cts = CancellationTokenSource.CreateLinkedTokenSource(this._serviceCancellationTokenSource.Token);
            cts.CancelAfter(expirationMs);
            cancellationToken = cts.Token;
        }

        int? n = default;
        try
        {
            var requestMessage = Encoding.UTF8.GetString(body);
            n = int.Parse(requestMessage);
            this._logger.LogInformation("Fib({n})", n);
            response = Fib(n.Value, cancellationToken).ToString(CultureInfo.InvariantCulture);
        }
        catch (OperationCanceledException ex)
        {
            this._logger.LogWarning("Fib({n}) - {message}", n, ex.Message);
            response = string.Empty;
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Fib({n}) - {message}", n, ex.Message);
            response = string.Empty;
        }
        finally
        {
            var responseBytes = Encoding.UTF8.GetBytes(response);
            channel.BasicPublish(exchange: string.Empty, routingKey: props.ReplyTo, basicProperties: replyProps, body: responseBytes);
            channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
            cts?.Dispose();
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _serviceCancellationTokenSource.Cancel();

        foreach (var channel in this._channels)
        {
            channel.Dispose();
        }
        this._connection?.Dispose();
        this._serviceCancellationTokenSource.Dispose();

        return Task.CompletedTask;
    }


    /// <remarks>
    /// Assumes only valid positive integer input.
    /// Don't expect this one to work for big numbers, and it's probably the slowest recursive implementation possible.
    /// </remarks>
    private static int Fib(int n, CancellationToken cancellationToken)
    {
        if (n is 0 or 1)
        {
            return n;
        }

        cancellationToken.ThrowIfCancellationRequested();

        return Fib(n - 1, cancellationToken) + Fib(n - 2, cancellationToken);
    }
}