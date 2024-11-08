using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace GrpcGreeter;

public class RabbitMqServer : IHostedService
{
    private const string QUEUE_NAME = "rpc_queue";

    private readonly FibService _fibService;
    private readonly ILogger<RabbitMqServer> _logger;

    private IConnection? _connection;
    private readonly List<IChannel> _channels = new();
    private ushort _parallelCount = 4;
    private readonly CancellationTokenSource _serviceCancellationTokenSource = new();
    private readonly string _replyExchangeName;

    public RabbitMqServer(FibService fibService, ILogger<RabbitMqServer> logger)
    {
        _fibService = fibService;
        _logger = logger;
        _replyExchangeName = $"{QUEUE_NAME}-response";
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        var factory = new ConnectionFactory
            { HostName = "localhost", UserName = "guest", Password = "guest", };
        _connection = await factory.CreateConnectionAsync(cancellationToken);

        for (int i = 0; i < _parallelCount; i++)
        {
            var channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);
            this._channels.Add(channel);

            if (i == 0)
            {
                await channel.QueueDeclareAsync(queue: QUEUE_NAME,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null,
                    cancellationToken: cancellationToken);
                await channel.ExchangeDeclareAsync(exchange: _replyExchangeName,
                    durable: true,
                    autoDelete: false,
                    arguments: null,
                    type: ExchangeType.Direct,
                    cancellationToken: cancellationToken);
            }

            await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, cancellationToken);

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += OnMessageReceivedAsync;
            await channel.BasicConsumeAsync(queue: QUEUE_NAME,
                autoAck: false,
                consumer: consumer,
                cancellationToken: cancellationToken);
        }

        this._logger.LogInformation("Awaiting RPC requests");
    }

    private async Task OnMessageReceivedAsync(object? consumer, BasicDeliverEventArgs ea)
    {
        var channel = (consumer as IAsyncBasicConsumer)?.Channel;
        if (channel == null || channel.IsClosed) throw new OperationCanceledException("Channel closed");
        var serviceShutdownToken = this._serviceCancellationTokenSource.Token;

        this._logger.LogInformation("Received RPC request");
        var call = new RabbitRpcRequestCall(channel, ea, this._replyExchangeName);
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