using MQTTnet;
using System;
using System.Buffers;
using System.Text.Json.Serialization.Metadata;
using System.Threading;

#nullable enable

namespace MQTTnet.AspNetCore.Routing
{
    /// <summary>
    /// payload 输入 formatter 选择和读取时使用的上下文。
    /// </summary>
    public sealed class MqttPayloadInputFormatterContext
    {
        /// <summary>
        /// 创建 payload 输入 formatter 上下文。
        /// </summary>
        /// <param name="actionContext">action 上下文。</param>
        /// <param name="modelType">目标模型类型。</param>
        /// <param name="jsonTypeInfo">JSON 类型元数据。</param>
        /// <param name="contentType">声明或消息携带的内容类型。</param>
        /// <param name="formatterName">显式 formatter 名称。</param>
        public MqttPayloadInputFormatterContext(
            MqttActionContext actionContext,
            Type modelType,
            JsonTypeInfo? jsonTypeInfo = null,
            string? contentType = null,
            string? formatterName = null)
        {
            ActionContext = actionContext ?? throw new ArgumentNullException(nameof(actionContext));
            ModelType = modelType ?? throw new ArgumentNullException(nameof(modelType));
            JsonTypeInfo = jsonTypeInfo;
            ContentType = contentType
                ?? actionContext.RequestContext.ContentType
                ?? actionContext.RouteContext.MatchedRoute?.DeclaredContentType;
            FormatterName = formatterName
                ?? actionContext.RouteContext.MatchedRoute?.DeclaredPayloadFormatterName;
        }

        /// <summary>
        /// action 上下文。
        /// </summary>
        public MqttActionContext ActionContext { get; }

        /// <summary>
        /// 目标模型类型。
        /// </summary>
        public Type ModelType { get; }

        /// <summary>
        /// JSON 类型元数据；非 JSON formatter 可忽略。
        /// </summary>
        public JsonTypeInfo? JsonTypeInfo { get; }

        /// <summary>
        /// 内容类型。
        /// </summary>
        public string? ContentType { get; }

        /// <summary>
        /// 显式 formatter 名称。
        /// </summary>
        public string? FormatterName { get; }

        /// <summary>
        /// 原始 MQTT 应用消息。
        /// </summary>
        public MqttApplicationMessage Message => ActionContext.RequestContext.Message;

        /// <summary>
        /// 原始 payload。
        /// </summary>
        public ReadOnlySequence<byte> Payload => ActionContext.RequestContext.Payload;

        /// <summary>
        /// 请求取消令牌。
        /// </summary>
        public CancellationToken CancellationToken => ActionContext.RequestContext.CancellationToken;
    }
}
