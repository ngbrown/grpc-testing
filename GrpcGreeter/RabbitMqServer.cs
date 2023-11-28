using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace GrpcGreeter;

public class RabbitMqServer : IHostedService
{
    private const string QUEUE_NAME = "rpc_queue";

    private readonly FibService _fibService;
    private readonly ILogger<RabbitMqServer> _logger;

    private IConnection? _connection;
    private readonly List<IModel> _channels = new();
    private ushort _parallelCount = 4;
    private readonly CancellationTokenSource _serviceCancellationTokenSource = new();

    public RabbitMqServer(FibService fibService, ILogger<RabbitMqServer> logger)
    {
        _fibService = fibService;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        var factory = new ConnectionFactory
            { HostName = "localhost", UserName = "guest", Password = "guest", DispatchConsumersAsync = true, };
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

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.Received += OnMessageReceivedAsync;
            channel.BasicConsume(queue: QUEUE_NAME,
                autoAck: false,
                consumer: consumer);
        }

        this._logger.LogInformation("Awaiting RPC requests");

        return Task.CompletedTask;
    }

    private async Task OnMessageReceivedAsync(object? consumer, BasicDeliverEventArgs ea)
    {
        var channel = (consumer as IBasicConsumer)?.Model;
        if (channel == null || channel.IsClosed) throw new OperationCanceledException("Channel closed");
        var serviceShutdownToken = this._serviceCancellationTokenSource.Token;

        this._logger.LogInformation("Received RPC request");
        var call = new RabbitRpcRequestCall(channel, ea);
        await call.DoCall(_fibService.GetFibAsync, serviceShutdownToken);
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
}