using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace GrpcGreeter;

public class RabbitRpcRequestCall
{
    private readonly IModel _channel;
    private readonly byte[] _body;
    private readonly IBasicProperties _props;
    private readonly ulong _deliveryTag;
    private readonly TimeSpan? _timeout;

    public RabbitRpcRequestCall(IModel channel, BasicDeliverEventArgs ea)
    {
        _channel = channel;
        _body = ea.Body.ToArray();
        _props = ea.BasicProperties;
        _deliveryTag = ea.DeliveryTag;

        if (!string.IsNullOrWhiteSpace(this._props.Expiration) &&
            int.TryParse(this._props.Expiration, out int expirationMs))
        {
            _timeout = TimeSpan.FromMilliseconds(expirationMs);
        }
    }

    public async Task DoCall(Func<byte[], CancellationToken, Task<byte[]>> method,
        CancellationToken serviceShutdownToken)
    {
        var replyProps = this._channel.CreateBasicProperties();
        replyProps.CorrelationId = this._props.CorrelationId;

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

        byte[] responseBytes = { 0x00 };
        try
        {
            responseBytes = await method(this._body, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            responseBytes = new byte[] { 0x00 };
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