using System;
using System.Buffers;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

#nullable enable

namespace MQTTnet.AspNetCore.Routing
{
    internal static class MqttJsonPayloadSerializer
    {
        public static object? Deserialize(ReadOnlySequence<byte> payload, JsonTypeInfo jsonTypeInfo)
        {
            if (payload.IsSingleSegment)
            {
                return JsonSerializer.Deserialize(payload.FirstSpan, jsonTypeInfo);
            }

            using var stream = new ReadOnlySequenceStream(payload);
            return JsonSerializer.Deserialize(stream, jsonTypeInfo);
        }

        public static TPayload? Deserialize<TPayload>(
            ReadOnlySequence<byte> payload,
            JsonTypeInfo<TPayload> jsonTypeInfo)
        {
            if (payload.IsSingleSegment)
            {
                return JsonSerializer.Deserialize(payload.FirstSpan, jsonTypeInfo);
            }

            using var stream = new ReadOnlySequenceStream(payload);
            return JsonSerializer.Deserialize(stream, jsonTypeInfo);
        }

        private sealed class ReadOnlySequenceStream : Stream
        {
            private readonly ReadOnlySequence<byte> _sequence;
            private SequencePosition _position;
            private long _consumed;

            public ReadOnlySequenceStream(ReadOnlySequence<byte> sequence)
            {
                _sequence = sequence;
                _position = sequence.Start;
            }

            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanWrite => false;

            public override long Length => _sequence.Length;

            public override long Position
            {
                get => _consumed;
                set => throw new NotSupportedException();
            }

            public override void Flush()
            {
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                return Read(buffer.AsSpan(offset, count));
            }

            public override int Read(Span<byte> buffer)
            {
                if (buffer.Length == 0 || _position.Equals(_sequence.End))
                {
                    return 0;
                }

                var remaining = _sequence.Slice(_position);
                var count = (int)Math.Min(buffer.Length, remaining.Length);
                remaining.Slice(0, count).CopyTo(buffer);
                _position = remaining.GetPosition(count);
                _consumed += count;
                return count;
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }
        }
    }
}
