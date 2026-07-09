using MQTTnet;
using MQTTnet.Protocol;
using System;
using System.Buffers;
using System.Linq;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;

#nullable enable

namespace MQTTnet.AspNetCore.Routing
{
    /// <summary>
    /// 表示 action 需要将 CLR payload 通过输出 formatter 发布到 MQTT topic。
    /// </summary>
    public class MqttPayloadResult : MqttResult
    {
        /// <summary>
        /// 创建 payload 发布结果。
        /// </summary>
        /// <param name="payload">要写出的 payload 对象。</param>
        /// <param name="payloadType">payload 的声明类型。</param>
        /// <param name="topic">响应 topic；为空时使用请求消息的 response topic。</param>
        /// <param name="disposition">当前入站 PUBLISH 的处置方式；为空表示保持调用前状态。</param>
        public MqttPayloadResult(
            object? payload,
            Type payloadType,
            string? topic = null,
            MqttInboundPublishDisposition? disposition = null)
            : base(disposition)
        {
            Payload = payload;
            PayloadType = payloadType ?? throw new ArgumentNullException(nameof(payloadType));
            Topic = topic;
        }

        /// <summary>
        /// 要写出的 payload 对象。
        /// </summary>
        public object? Payload { get; }

        /// <summary>
        /// payload 的声明类型。
        /// </summary>
        public Type PayloadType { get; }

        /// <summary>
        /// 响应 topic；为空时使用请求消息的 response topic。
        /// </summary>
        public string? Topic { get; }

        /// <summary>
        /// 响应消息的 content type；为空时由 formatter 或 route 元数据决定。
        /// </summary>
        public string? ContentType { get; init; }

        /// <summary>
        /// 显式输出 formatter 名称。
        /// </summary>
        public string? FormatterName { get; init; }

        /// <summary>
        /// 响应消息 QoS；为空时使用 QoS 0。
        /// </summary>
        public MqttQualityOfServiceLevel? QualityOfServiceLevel { get; init; }

        /// <summary>
        /// 响应消息 retain 标志。
        /// </summary>
        public bool Retain { get; init; }

        /// <summary>
        /// 响应消息 correlation data；为空时沿用请求消息的 correlation data。
        /// </summary>
        public byte[]? CorrelationData { get; init; }

        /// <inheritdoc />
        public override async ValueTask ExecuteAsync(MqttActionContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            ApplyInboundPublishDisposition(context);

            var topic = ResolveTopic(context);
            var options = context.RoutingOptions
                ?? throw new InvalidOperationException("MqttPayloadResult requires MqttRoutingOptions to select an output formatter.");
            var formatterContext = new MqttPayloadOutputFormatterContext(
                context,
                Payload,
                PayloadType,
                ResolveJsonTypeInfo(options),
                ContentType ?? options.DefaultPayloadContentType,
                FormatterName ?? options.DefaultPayloadFormatterName);
            var formatter = options.OutputFormatters.FirstOrDefault(item => item.CanWrite(formatterContext));
            if (formatter == null)
            {
                throw new InvalidOperationException($"No MQTT payload formatter can write payload type '{PayloadType.FullName}'.");
            }

            var writeResult = await formatter.WriteAsync(formatterContext).ConfigureAwait(false);
            var builder = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(new ReadOnlySequence<byte>(writeResult.Payload))
                .WithQualityOfServiceLevel(QualityOfServiceLevel ?? MqttQualityOfServiceLevel.AtMostOnce)
                .WithRetainFlag(Retain);

            var contentType = writeResult.ContentType ?? ContentType;
            if (!string.IsNullOrWhiteSpace(contentType))
            {
                builder.WithContentType(contentType);
            }

            var correlationData = CorrelationData ?? context.RequestContext.CorrelationData;
            if (correlationData is { Length: > 0 })
            {
                builder.WithCorrelationData(correlationData);
            }

            var publishResult = new MqttPublishResult(builder.Build());
            await publishResult.ExecuteAsync(context).ConfigureAwait(false);
        }

        private string ResolveTopic(MqttActionContext context)
        {
            if (!string.IsNullOrWhiteSpace(Topic))
            {
                return Topic!;
            }

            if (!string.IsNullOrWhiteSpace(context.RequestContext.ResponseTopic))
            {
                return context.RequestContext.ResponseTopic!;
            }

            throw new InvalidOperationException(
                "MqttPayloadResult requires an explicit topic or an MQTT v5 response topic on the request message.");
        }

        private JsonTypeInfo? ResolveJsonTypeInfo(MqttRoutingOptions options)
        {
            var jsonTypeInfo = options.SerializerContext?.GetTypeInfo(PayloadType);
            if (jsonTypeInfo != null)
            {
                return jsonTypeInfo;
            }

            try
            {
                return options.SerializerOptions?.GetTypeInfo(PayloadType);
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is NotSupportedException)
            {
                return null;
            }
        }
    }

    /// <summary>
    /// 表示 action 需要将强类型 payload 通过输出 formatter 发布到 MQTT topic。
    /// </summary>
    /// <typeparam name="TPayload">payload 类型。</typeparam>
    public sealed class MqttPayloadResult<TPayload> : MqttPayloadResult
    {
        /// <summary>
        /// 创建强类型 payload 发布结果。
        /// </summary>
        /// <param name="payload">要写出的 payload 对象。</param>
        /// <param name="topic">响应 topic；为空时使用请求消息的 response topic。</param>
        /// <param name="disposition">当前入站 PUBLISH 的处置方式；为空表示保持调用前状态。</param>
        public MqttPayloadResult(
            TPayload payload,
            string? topic = null,
            MqttInboundPublishDisposition? disposition = null)
            : base(payload, typeof(TPayload), topic, disposition)
        {
        }
    }
}
