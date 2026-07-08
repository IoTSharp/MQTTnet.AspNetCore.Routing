using System;

namespace MQTTnet.AspNetCore.Routing.Attributes;

/// <summary>
/// 指示参数从 MQTT 执行上下文绑定。
/// </summary>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
public sealed class FromMqttContextAttribute : Attribute
{
}
