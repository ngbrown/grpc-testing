using Google.Protobuf;
using Grpc.Core;
using GrpcGreeter.RabbitGrpc.Server;
using GrpcGreeter.RabbitGrpc.Server.Internal;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitRpc.Core;
using Status = RabbitRpc.Core.Google.Status;

namespace GrpcGreeter;

public class RabbitRpcRequestCall
{
    private readonly IModel _channel;
    private readonly IServiceProvider _requestServices;
    private readonly byte[] _body;
    private readonly IBasicProperties _props;
    private readonly ulong _deliveryTag;
    private readonly TimeSpan? _timeout;

    public RabbitRpcRequestCall(IModel channel, BasicDeliverEventArgs ea, IServiceProvider requestServices)
    {
        _channel = channel;
        _requestServices = requestServices;
        _body = ea.Body.ToArray();
        _props = ea.BasicProperties;
        _deliveryTag = ea.DeliveryTag;

        if (!string.IsNullOrWhiteSpace(this._props.Expiration) &&
            int.TryParse(this._props.Expiration, out int expirationMs))
        {
            _timeout = TimeSpan.FromMilliseconds(expirationMs);
        }
    }

    public async Task DoCall(MessageDelegate messageDelegate, CancellationToken serviceShutdownToken)
    {
        var replyProps = this._channel.CreateBasicProperties();
        replyProps.CorrelationId = this._props.CorrelationId;
        replyProps.ContentType = RabbitGrpcProtocolConstants.ResponseContentType;

        CancellationTokenSource? cts = default;
        CancellationToken cancellationToken;
        if (this._timeout.HasValue)
        {
            cts = CancellationTokenSource.CreateLinkedTokenSource(serviceShutdownToken);
            cts.CancelAfter(this._timeout.Value);
            cancellationToken = cts.Token;
        }
        else
        {
            cancellationToken = serviceShutdownToken;
        }

        byte[] responseBytes;
        try
        {
            var rpcRequest = new RabbitRpcRequest();
            rpcRequest.MergeFrom(_body);

            var context = new RpcContext(rpcRequest, _requestServices, cancellationToken);
            await messageDelegate(context).ConfigureAwait(false);

            responseBytes = context.Response.ToByteArray();
        }
        catch (Exception ex)
        {
            responseBytes = new RabbitRpcResponse
                { Status = new Status { Code = (int)StatusCode.Internal, Message = ex.Message } }.ToByteArray();
        }
        finally
        {
            cts?.Dispose();
        }

        serviceShutdownToken.ThrowIfCancellationRequested();
        if (_channel == null || _channel.IsClosed) throw new OperationCanceledException("Channel closed");

        replyProps.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        this._channel.BasicPublish(exchange: string.Empty, routingKey: this._props.ReplyTo, basicProperties: replyProps,
            body: responseBytes);
        this._channel.BasicAck(deliveryTag: _deliveryTag, multiple: false);
    }
}