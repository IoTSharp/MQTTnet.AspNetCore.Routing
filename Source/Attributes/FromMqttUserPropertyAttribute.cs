using System;

#nullable enable

namespace MQTTnet.AspNetCore.Routing.Attributes;

/// <summary>
/// 指示参数从 MQTT v5 user property 绑定。
/// </summary>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
public sealed class FromMqttUserPropertyAttribute : Attribute
{
    /// <summary>
    /// 创建 user property 绑定声明。
    /// </summary>
    public FromMqttUserPropertyAttribute()
    {
    }

    /// <summary>
    /// 创建 user property 绑定声明。
    /// </summary>
    /// <param name="name">user property 名称；为空时使用参数名。</param>
    public FromMqttUserPropertyAttribute(string name)
    {
        Name = name;
    }

    /// <summary>
    /// user property 名称；为空时使用参数名。
    /// </summary>
    public string? Name { get; }
}
