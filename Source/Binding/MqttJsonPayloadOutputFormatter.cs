using System;
using System.Text.Json;
using System.Threading.Tasks;

#nullable enable

namespace MQTTnet.AspNetCore.Routing
{
    /// <summary>
    /// 基于 System.Text.Json 的 payload 输出 formatter。
    /// </summary>
    public sealed class MqttJsonPayloadOutputFormatter : IMqttPayloadOutputFormatter
    {
        /// <summary>
        /// formatter 名称。
        /// </summary>
        public string Name => "json";

        /// <inheritdoc />
        public bool CanWrite(MqttPayloadOutputFormatterContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (!string.IsNullOrEmpty(context.FormatterName)
                && !string.Equals(context.FormatterName, Name, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (context.JsonTypeInfo == null || MqttBinaryPayloadInputFormatter.IsRawPayloadType(context.PayloadType))
            {
                return false;
            }

            return MqttJsonPayloadInputFormatter.IsJsonContentType(context.ContentType);
        }

        /// <inheritdoc />
        public ValueTask<MqttPayloadWriteResult> WriteAsync(MqttPayloadOutputFormatterContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (context.JsonTypeInfo == null)
            {
                throw new InvalidOperationException("No JSON type metadata is configured for the MQTT payload.");
            }

            var payload = JsonSerializer.SerializeToUtf8Bytes(context.Value, context.JsonTypeInfo);
            return ValueTask.FromResult(new MqttPayloadWriteResult(
                payload,
                context.ContentType ?? "application/json"));
        }
    }
}
