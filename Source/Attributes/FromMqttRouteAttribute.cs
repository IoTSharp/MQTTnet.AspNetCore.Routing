using System;

#nullable enable

namespace MQTTnet.AspNetCore.Routing.Attributes;

/// <summary>
/// 指示参数从 MQTT topic route value 绑定。
/// </summary>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
public sealed class FromMqttRouteAttribute : Attribute
{
    /// <summary>
    /// 创建 route value 绑定声明。
    /// </summary>
    public FromMqttRouteAttribute()
    {
    }

    /// <summary>
    /// 创建 route value 绑定声明。
    /// </summary>
    /// <param name="name">route value 名称；为空时使用参数名。</param>
    public FromMqttRouteAttribute(string name)
    {
        Name = name;
    }

    /// <summary>
    /// route value 名称；为空时使用参数名。
    /// </summary>
    public string? Name { get; }
}
