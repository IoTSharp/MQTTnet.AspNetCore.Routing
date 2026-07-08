using System;
using System.Buffers;
using System.Threading.Tasks;

#nullable enable

namespace MQTTnet.AspNetCore.Routing
{
    /// <summary>
    /// 写出原始二进制 payload 的输出 formatter。
    /// </summary>
    public sealed class MqttBinaryPayloadOutputFormatter : IMqttPayloadOutputFormatter
    {
        /// <summary>
        /// formatter 名称。
        /// </summary>
        public string Name => "binary";

        /// <inheritdoc />
        public bool CanWrite(MqttPayloadOutputFormatterContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (!MqttBinaryPayloadInputFormatter.IsRawPayloadType(context.PayloadType))
            {
                return false;
            }

            return string.IsNullOrEmpty(context.FormatterName)
                || string.Equals(context.FormatterName, Name, StringComparison.OrdinalIgnoreCase);
        }

        /// <inheritdoc />
        public ValueTask<MqttPayloadWriteResult> WriteAsync(MqttPayloadOutputFormatterContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (context.Value is byte[] bytes)
            {
                return ValueTask.FromResult(new MqttPayloadWriteResult(bytes, context.ContentType));
            }

            if (context.Value is ReadOnlyMemory<byte> memory)
            {
                return ValueTask.FromResult(new MqttPayloadWriteResult(memory, context.ContentType));
            }

            if (context.Value is ReadOnlySequence<byte> sequence)
            {
                var payload = sequence.IsSingleSegment
                    ? sequence.First
                    : sequence.ToArray();
                return ValueTask.FromResult(new MqttPayloadWriteResult(payload, context.ContentType));
            }

            throw new InvalidOperationException($"Payload type '{context.PayloadType.FullName}' is not supported by the binary formatter.");
        }
    }
}
