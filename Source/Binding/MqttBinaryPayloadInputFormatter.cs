using System;
using System.Buffers;
using System.Threading.Tasks;

#nullable enable

namespace MQTTnet.AspNetCore.Routing
{
    /// <summary>
    /// 读取原始二进制 payload 的输入 formatter。
    /// </summary>
    public sealed class MqttBinaryPayloadInputFormatter : IMqttPayloadInputFormatter
    {
        /// <summary>
        /// formatter 名称。
        /// </summary>
        public string Name => "binary";

        /// <inheritdoc />
        public bool CanRead(MqttPayloadInputFormatterContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (!IsRawPayloadType(context.ModelType))
            {
                return false;
            }

            return string.IsNullOrEmpty(context.FormatterName)
                || string.Equals(context.FormatterName, Name, StringComparison.OrdinalIgnoreCase);
        }

        /// <inheritdoc />
        public ValueTask<object?> ReadAsync(MqttPayloadInputFormatterContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (context.ModelType == typeof(ReadOnlySequence<byte>))
            {
                return ValueTask.FromResult<object?>(context.Payload);
            }

            if (context.ModelType == typeof(ReadOnlyMemory<byte>))
            {
                var memory = context.Payload.IsSingleSegment
                    ? context.Payload.First
                    : context.Payload.ToArray();
                return ValueTask.FromResult<object?>(memory);
            }

            if (context.ModelType == typeof(byte[]))
            {
                return ValueTask.FromResult<object?>(context.Payload.ToArray());
            }

            throw new InvalidOperationException($"Payload type '{context.ModelType.FullName}' is not supported by the binary formatter.");
        }

        internal static bool IsRawPayloadType(Type type)
        {
            return type == typeof(byte[])
                || type == typeof(ReadOnlyMemory<byte>)
                || type == typeof(ReadOnlySequence<byte>);
        }
    }
}
