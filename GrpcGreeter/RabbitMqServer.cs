using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace GrpcGreeter;

public class RabbitMqServer : IHostedService
{
    private readonly ILogger<RabbitMqServer> _logger;

    private IConnection? _connection;
    private IModel? _channel;

    public RabbitMqServer(ILogger<RabbitMqServer> logger)
    {
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        var factory = new ConnectionFactory { HostName = "localhost", UserName = "guest", Password = "guest" };
        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        _channel.QueueDeclare(queue: "rpc_queue",
            durable: false,
            exclusive: false,
            autoDelete: false,
            arguments: null);
        _channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);
        var consumer = new EventingBasicConsumer(_channel);
        _channel.BasicConsume(queue: "rpc_queue",
            autoAck: false,
            consumer: consumer);

        this._logger.LogInformation("Awaiting RPC requests");

        consumer.Received += OnMessageReceived;

        return Task.CompletedTask;
    }

    private void OnMessageReceived(object? model, BasicDeliverEventArgs ea)
    {
        var channel = _channel;
        if (channel == null || channel.IsClosed) throw new OperationCanceledException();

        string response = string.Empty;

        var body = ea.Body.ToArray();
        var props = ea.BasicProperties;
        var replyProps = channel.CreateBasicProperties();
        replyProps.CorrelationId = props.CorrelationId;

        try
        {
            var message = Encoding.UTF8.GetString(body);
            int n = int.Parse(message);
            this._logger.LogInformation("Fib({message})", message);
            response = Fib(n).ToString();
        }
        catch (Exception e)
        {
            this._logger.LogError(e, "{message}", e.Message);
            response = string.Empty;
        }
        finally
        {
            var responseBytes = Encoding.UTF8.GetBytes(response);
            channel.BasicPublish(exchange: string.Empty, routingKey: props.ReplyTo, basicProperties: replyProps, body: responseBytes);
            channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        this._channel?.Dispose();
        this._connection?.Dispose();

        return Task.CompletedTask;
    }


    /// <remarks>
    /// Assumes only valid positive integer input.
    /// Don't expect this one to work for big numbers, and it's probably the slowest recursive implementation possible.
    /// </remarks>
    private static int Fib(int n)
    {
        if (n is 0 or 1)
        {
            return n;
        }

        return Fib(n - 1) + Fib(n - 2);
    }
}