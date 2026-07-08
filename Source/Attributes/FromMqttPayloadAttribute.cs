using System;

#nullable enable

namespace MQTTnet.AspNetCore.Routing.Attributes;

/// <summary>
/// 指示参数从 MQTT payload 绑定。
/// </summary>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
public sealed class FromMqttPayloadAttribute : Attribute
{
    /// <summary>
    /// 创建 payload 绑定声明。
    /// </summary>
    public FromMqttPayloadAttribute()
    {
    }

    /// <summary>
    /// 创建 payload 绑定声明。
    /// </summary>
    /// <param name="contentType">参数声明的内容类型，用于选择 payload formatter。</param>
    public FromMqttPayloadAttribute(string contentType)
    {
        ContentType = contentType;
    }

    /// <summary>
    /// 参数声明的内容类型；为空时使用 MQTT v5 content type 或默认 formatter。
    /// </summary>
    public string? ContentType { get; set; }

    /// <summary>
    /// 显式 formatter 名称；为空时按内容类型和默认规则选择。
    /// </summary>
    public string? FormatterName { get; set; }
}
