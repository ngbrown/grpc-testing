using System.Buffers;
using Google.Protobuf;
using Grpc.Core;

namespace GrpcGreeter.RabbitGrpc.Server.Internal;

internal sealed class ByteStringDeserializationContext : DeserializationContext
{
    private readonly ByteString _responseBody;

    public ByteStringDeserializationContext(ByteString responseBody)
    {
        _responseBody = responseBody;
    }

    public override int PayloadLength => _responseBody.Length;

    public override byte[] PayloadAsNewBuffer()
    {
        return _responseBody.ToByteArray();
    }

    public override ReadOnlySequence<byte> PayloadAsReadOnlySequence()
    {
        return new ReadOnlySequence<byte>(_responseBody.Memory);
    }
}