using MQTTnet.AspNetCore.Routing.Routing;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MQTTnet.AspNetCore.Routing;

public class MqttRoutingOptions

{
    public MqttRoutingOptions()
    {
        InputFormatters = new List<IMqttPayloadInputFormatter>();
        OutputFormatters = new List<IMqttPayloadOutputFormatter>();
    }

    public JsonSerializerOptions SerializerOptions { get;internal set; }
    public Assembly[] FromAssemblies { get; internal set; }
    public JsonSerializerContext SerializerContext { get; internal set; }
    public Type[] ControllerTypes { get; internal set; }
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    public Type RouteInvocationInterceptor { get; internal set; }
    /// <summary>
    /// MQTT payload 输入 formatter。默认包含 binary 与 JSON formatter。
    /// </summary>
    public IList<IMqttPayloadInputFormatter> InputFormatters { get; }

    /// <summary>
    /// MQTT payload 输出 formatter。默认包含 binary 与 JSON formatter。
    /// </summary>
    public IList<IMqttPayloadOutputFormatter> OutputFormatters { get; }

 
}
