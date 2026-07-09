using MQTTnet;
using MQTTnet.Protocol;
using System;
using System.Buffers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

#nullable enable

namespace MQTTnet.AspNetCore.Routing.Testing
{
    /// <summary>
    /// 用于单元测试的 MQTT 应用消息工厂。
    /// </summary>
    public static class MqttTestMessages
    {
        /// <summary>
        /// 创建 UTF-8 文本 payload 的 MQTT 应用消息。
        /// </summary>
        /// <param name="topic">消息 topic。</param>
        /// <param name="payload">文本 payload；为空时使用空字符串。</param>
        /// <param name="contentType">MQTT v5 content type。</param>
        /// <param name="responseTopic">MQTT v5 response topic。</param>
        /// <param name="correlationData">MQTT v5 correlation data。</param>
        /// <param name="qualityOfServiceLevel">消息 QoS。</param>
        /// <param name="retain">retain 标志。</param>
        /// <param name="userProperties">MQTT v5 user properties。</param>
        public static MqttApplicationMessage Create(
            string topic,
            string? payload = null,
            string? contentType = null,
            string? responseTopic = null,
            byte[]? correlationData = null,
            MqttQualityOfServiceLevel qualityOfServiceLevel = MqttQualityOfServiceLevel.AtMostOnce,
            bool retain = false,
            params (string Name, string Value)[] userProperties)
        {
            return CreateBinary(
                topic,
                Encoding.UTF8.GetBytes(payload ?? string.Empty),
                contentType,
                responseTopic,
                correlationData,
                qualityOfServiceLevel,
                retain,
                userProperties);
        }

        /// <summary>
        /// 创建 JSON payload 的 MQTT 应用消息。
        /// </summary>
        /// <typeparam name="TPayload">payload 类型。</typeparam>
        /// <param name="topic">消息 topic。</param>
        /// <param name="payload">要序列化的 payload。</param>
        /// <param name="jsonTypeInfo">source-generated JSON 类型元数据。</param>
        /// <param name="contentType">MQTT v5 content type；为空时使用 application/json。</param>
        /// <param name="responseTopic">MQTT v5 response topic。</param>
        /// <param name="correlationData">MQTT v5 correlation data。</param>
        /// <param name="qualityOfServiceLevel">消息 QoS。</param>
        /// <param name="retain">retain 标志。</param>
        /// <param name="userProperties">MQTT v5 user properties。</param>
        public static MqttApplicationMessage CreateJson<TPayload>(
            string topic,
            TPayload payload,
            JsonTypeInfo<TPayload> jsonTypeInfo,
            string? contentType = "application/json",
            string? responseTopic = null,
            byte[]? correlationData = null,
            MqttQualityOfServiceLevel qualityOfServiceLevel = MqttQualityOfServiceLevel.AtMostOnce,
            bool retain = false,
            params (string Name, string Value)[] userProperties)
        {
            if (jsonTypeInfo == null)
            {
                throw new ArgumentNullException(nameof(jsonTypeInfo));
            }

            return CreateBinary(
                topic,
                JsonSerializer.SerializeToUtf8Bytes(payload, jsonTypeInfo),
                contentType,
                responseTopic,
                correlationData,
                qualityOfServiceLevel,
                retain,
                userProperties);
        }

        /// <summary>
        /// 创建二进制 payload 的 MQTT 应用消息。
        /// </summary>
        /// <param name="topic">消息 topic。</param>
        /// <param name="payload">二进制 payload。</param>
        /// <param name="contentType">MQTT v5 content type。</param>
        /// <param name="responseTopic">MQTT v5 response topic。</param>
        /// <param name="correlationData">MQTT v5 correlation data。</param>
        /// <param name="qualityOfServiceLevel">消息 QoS。</param>
        /// <param name="retain">retain 标志。</param>
        /// <param name="userProperties">MQTT v5 user properties。</param>
        public static MqttApplicationMessage CreateBinary(
            string topic,
            ReadOnlyMemory<byte> payload,
            string? contentType = null,
            string? responseTopic = null,
            byte[]? correlationData = null,
            MqttQualityOfServiceLevel qualityOfServiceLevel = MqttQualityOfServiceLevel.AtMostOnce,
            bool retain = false,
            params (string Name, string Value)[] userProperties)
        {
            if (topic == null)
            {
                throw new ArgumentNullException(nameof(topic));
            }

            var builder = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(new ReadOnlySequence<byte>(payload))
                .WithQualityOfServiceLevel(qualityOfServiceLevel)
                .WithRetainFlag(retain);

            if (!string.IsNullOrWhiteSpace(contentType))
            {
                builder.WithContentType(contentType);
            }

            if (!string.IsNullOrWhiteSpace(responseTopic))
            {
                builder.WithResponseTopic(responseTopic);
            }

            if (correlationData is { Length: > 0 })
            {
                builder.WithCorrelationData(correlationData);
            }

            if (userProperties != null)
            {
                foreach (var (name, value) in userProperties)
                {
                    builder.WithUserProperty(name, Encoding.UTF8.GetBytes(value ?? string.Empty));
                }
            }

            return builder.Build();
        }
    }
}
