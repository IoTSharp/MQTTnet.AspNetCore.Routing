using System;

#nullable enable

namespace MQTTnet.AspNetCore.Routing.Attributes
{
    /// <summary>
    /// 标记由 MQTT source generator 生成无反射 route 和 action 委托的 controller。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class MqttGeneratedControllerAttribute : Attribute
    {
    }
}
