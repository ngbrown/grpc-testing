using Google.Protobuf;
using Grpc.Core;
using GrpcGreeter.RabbitGrpc.Server.Internal;
using GrpcGreeter.RabbitGrpc.Server.Model.Internal;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitRpc.Core;

namespace GrpcGreeter;

internal class RabbitMqServer : IHostedService
{
    private const string QUEUE_NAME = "rpc_queue";

    private readonly ServiceMethodsRegistry _serviceMethodsRegistry;
    private readonly ILogger<RabbitMqServer> _logger;

    private IConnection? _connection;
    private readonly List<IModel> _channels = new();
    private ushort _parallelCount = 4;
    private readonly CancellationTokenSource _serviceCancellationTokenSource = new();
    private IServiceProvider _serviceProvider;

    public RabbitMqServer(ServiceMethodsRegistry serviceMethodsRegistry, ILogger<RabbitMqServer> logger, IServiceProvider serviceProvider)
    {
        _serviceMethodsRegistry = serviceMethodsRegistry;
        _logger = logger;
        _serviceProvider = serviceProvider;
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
        if (channel == null || channel.IsClosed) throw new OperationCanceledException("Channel closed");
        var serviceShutdownToken = this._serviceCancellationTokenSource.Token;

        var body = ea.Body.ToArray();

        try
        {
            var request = new RabbitRpcRequest();
            request.MergeFrom(body);

            var methodModel = _serviceMethodsRegistry.Methods.Find(m => m.Method.ServiceName == request.ServiceName && m.Method.FullName == request.MethodName);
            if (methodModel != null)
            {
                var call = new RabbitRpcRequestCall(channel, ea, _serviceProvider);
                var _ = call.DoCall(methodModel.RequestDelegate, serviceShutdownToken);
            }
            else
            {
                ReplyNotFound(channel, ea.BasicProperties, ea.DeliveryTag);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
    }

    private static void ReplyNotFound(IModel channel, IBasicProperties props, ulong deliveryTag)
    {
        var replyProps = channel.CreateBasicProperties();
        replyProps.CorrelationId = props.CorrelationId;
        replyProps.ContentType = RabbitGrpcProtocolConstants.ResponseContentType;
        var responseBytes = new RabbitRpcResponse
            { Status = new RabbitRpc.Core.Google.Status { Code = (int)StatusCode.NotFound, } }.ToByteArray();
        channel.BasicPublish(exchange: string.Empty, routingKey: props.ReplyTo, basicProperties: replyProps,
            body: responseBytes);
        channel.BasicAck(deliveryTag: deliveryTag, multiple: false);
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