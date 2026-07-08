using System;
using System.Text.Json;
using System.Threading.Tasks;

#nullable enable

namespace MQTTnet.AspNetCore.Routing
{
    /// <summary>
    /// 基于 System.Text.Json 的 payload 输入 formatter。
    /// </summary>
    public sealed class MqttJsonPayloadInputFormatter : IMqttPayloadInputFormatter
    {
        /// <summary>
        /// formatter 名称。
        /// </summary>
        public string Name => "json";

        /// <inheritdoc />
        public bool CanRead(MqttPayloadInputFormatterContext context)
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

            if (context.JsonTypeInfo == null || MqttBinaryPayloadInputFormatter.IsRawPayloadType(context.ModelType))
            {
                return false;
            }

            return IsJsonContentType(context.ContentType);
        }

        /// <inheritdoc />
        public ValueTask<object?> ReadAsync(MqttPayloadInputFormatterContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (context.JsonTypeInfo == null)
            {
                context.ActionContext.ModelState.AddModelError(
                    "$payload",
                    MqttBindingErrorCode.PayloadFormatterNotFound,
                    "No JSON type metadata is configured for the MQTT payload.");
                throw new MqttBindingException(
                    context.ActionContext.ModelState,
                    "No JSON type metadata is configured for the MQTT payload.");
            }

            try
            {
                return ValueTask.FromResult(MqttJsonPayloadSerializer.Deserialize(
                    context.Payload,
                    context.JsonTypeInfo));
            }
            catch (JsonException ex)
            {
                context.ActionContext.ModelState.AddModelError(
                    "$payload",
                    MqttBindingErrorCode.PayloadDeserializationFailed,
                    "MQTT payload could not be deserialized.");
                throw new MqttBindingException(
                    context.ActionContext.ModelState,
                    "MQTT payload could not be deserialized.",
                    ex);
            }
        }

        internal static bool IsJsonContentType(string? contentType)
        {
            if (string.IsNullOrWhiteSpace(contentType))
            {
                return true;
            }

            var mediaType = contentType.Split(';')[0].Trim();
            return string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase)
                || mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase);
        }
    }
}
