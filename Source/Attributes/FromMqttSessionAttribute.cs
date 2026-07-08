using System;

#nullable enable

namespace MQTTnet.AspNetCore.Routing.Attributes;

/// <summary>
/// 指示参数从 MQTT server session items 绑定。
/// </summary>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
public sealed class FromMqttSessionAttribute : Attribute
{
    /// <summary>
    /// 创建 session item 绑定声明。
    /// </summary>
    public FromMqttSessionAttribute()
    {
    }

    /// <summary>
    /// 创建 session item 绑定声明。
    /// </summary>
    /// <param name="key">session item key；为空时使用参数名。</param>
    public FromMqttSessionAttribute(string key)
    {
        Key = key;
    }

    /// <summary>
    /// session item key；为空时使用参数名。
    /// </summary>
    public string? Key { get; }
}
