using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Globalization;

namespace GrpcGreeter;

public class RabbitRpcRequestCall
{
    private readonly IChannel _channel;
    private readonly string _replyExchangeName;
    private readonly byte[] _body;
    private readonly IReadOnlyBasicProperties _props;
    private readonly ulong _deliveryTag;
    private readonly TimeSpan? _timeout;
    
    public TimeSpan DefaultResponseTimeout { get; set; } = TimeSpan.FromSeconds(10);

    public RabbitRpcRequestCall(IChannel channel, BasicDeliverEventArgs ea, string replyExchangeName)
    {
        _channel = channel;
        _replyExchangeName = replyExchangeName;
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

        var replyProps = CreateResponseProperties(this._props);
        await this._channel.BasicPublishAsync(exchange: this._replyExchangeName, routingKey: this._props.ReplyTo, basicProperties: replyProps,
            body: responseBytes, mandatory: false, cancellationToken: serviceShutdownToken);
        await this._channel.BasicAckAsync(deliveryTag: _deliveryTag, multiple: false, serviceShutdownToken);
    }

    private BasicProperties CreateResponseProperties(IReadOnlyBasicProperties props)
    {
        var replyProps = new BasicProperties(props)
        {
            CorrelationId = props.CorrelationId,
            Expiration = ((long)Math.Ceiling(this.DefaultResponseTimeout.TotalMilliseconds)).ToString(CultureInfo.InvariantCulture),
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        };
        return replyProps;
    }
}