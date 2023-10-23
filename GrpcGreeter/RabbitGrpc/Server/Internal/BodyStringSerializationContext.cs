using System.Buffers;
using Google.Protobuf;
using Grpc.Core;

namespace GrpcGreeter.RabbitGrpc.Server.Internal;

internal sealed class BodyStringSerializationContext : SerializationContext
{
    private readonly Action<ByteString> _setContent;
    private ArrayBufferWriter<byte>? _bufferWriter;
    private int? _payloadLength;

    public BodyStringSerializationContext(Action<ByteString> setContent)
    {
        _setContent = setContent;
    }

    public override void SetPayloadLength(int payloadLength)
    {
        _payloadLength = payloadLength;
    }

    public override IBufferWriter<byte> GetBufferWriter()
    {
        if (_bufferWriter == null)
        {
            // Initialize buffer writer with exact length if available.
            // ArrayBufferWriter doesn't allow zero initial length.
            _bufferWriter = _payloadLength > 0
                ? new ArrayBufferWriter<byte>(_payloadLength.Value)
                : new ArrayBufferWriter<byte>();
        }

        return _bufferWriter;
    }

    public override void Complete(byte[] payload)
    {
        _setContent(ByteString.CopyFrom(payload));
    }

    public override void Complete()
    {
        if (_bufferWriter == null)
        {
            throw new InvalidOperationException("No data written to BufferWriter");
        }

        _setContent(ByteString.CopyFrom(_bufferWriter.WrittenMemory.Span));
    }
}