using System;
using System.Text.Json.Serialization.Metadata;
using System.Threading;

#nullable enable

namespace MQTTnet.AspNetCore.Routing
{
    /// <summary>
    /// payload 输出 formatter 选择和写入时使用的上下文。
    /// </summary>
    public sealed class MqttPayloadOutputFormatterContext
    {
        /// <summary>
        /// 创建 payload 输出 formatter 上下文。
        /// </summary>
        /// <param name="actionContext">action 上下文。</param>
        /// <param name="value">要写出的值。</param>
        /// <param name="payloadType">要写出的值类型。</param>
        /// <param name="jsonTypeInfo">JSON 类型元数据。</param>
        /// <param name="contentType">目标内容类型。</param>
        /// <param name="formatterName">显式 formatter 名称。</param>
        public MqttPayloadOutputFormatterContext(
            MqttActionContext actionContext,
            object? value,
            Type payloadType,
            JsonTypeInfo? jsonTypeInfo = null,
            string? contentType = null,
            string? formatterName = null)
        {
            ActionContext = actionContext ?? throw new ArgumentNullException(nameof(actionContext));
            Value = value;
            PayloadType = payloadType ?? throw new ArgumentNullException(nameof(payloadType));
            JsonTypeInfo = jsonTypeInfo;
            ContentType = contentType
                ?? actionContext.RouteContext.MatchedRoute?.DeclaredContentType;
            FormatterName = formatterName
                ?? actionContext.RouteContext.MatchedRoute?.DeclaredPayloadFormatterName;
        }

        /// <summary>
        /// action 上下文。
        /// </summary>
        public MqttActionContext ActionContext { get; }

        /// <summary>
        /// 要写出的值。
        /// </summary>
        public object? Value { get; }

        /// <summary>
        /// 要写出的值类型。
        /// </summary>
        public Type PayloadType { get; }

        /// <summary>
        /// JSON 类型元数据；非 JSON formatter 可忽略。
        /// </summary>
        public JsonTypeInfo? JsonTypeInfo { get; }

        /// <summary>
        /// 目标内容类型。
        /// </summary>
        public string? ContentType { get; }

        /// <summary>
        /// 显式 formatter 名称。
        /// </summary>
        public string? FormatterName { get; }

        /// <summary>
        /// 请求取消令牌。
        /// </summary>
        public CancellationToken CancellationToken => ActionContext.RequestContext.CancellationToken;
    }
}
