using System;

#nullable enable

namespace MQTTnet.AspNetCore.Routing.Attributes;

/// <summary>
/// 指示参数从 MQTT client 信息绑定。
/// </summary>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
public sealed class FromMqttClientAttribute : Attribute
{
    /// <summary>
    /// 创建 client 信息绑定声明。
    /// </summary>
    public FromMqttClientAttribute()
    {
    }

    /// <summary>
    /// 创建 client 信息绑定声明。
    /// </summary>
    /// <param name="name">client 信息名称，当前支持 clientId 和 userName；为空时使用 clientId。</param>
    public FromMqttClientAttribute(string name)
    {
        Name = name;
    }

    /// <summary>
    /// client 信息名称，当前支持 clientId 和 userName；为空时使用 clientId。
    /// </summary>
    public string? Name { get; }
}
