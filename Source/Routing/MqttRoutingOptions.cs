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
        Filters = new List<MqttFilterModel>();
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

    /// <summary>
    /// 全局 MQTT filter。执行时会与 controller/action 上声明的 filter 合并并按 Order 排序。
    /// </summary>
    public IList<MqttFilterModel> Filters { get; }

    /// <summary>
    /// 当参数、MQTT v5 content type 和 route metadata 都未声明内容类型时使用的默认 payload content type。
    /// </summary>
    public string DefaultPayloadContentType { get; set; }

    /// <summary>
    /// 当参数和 route metadata 都未声明 formatter 名称时使用的默认 payload formatter 名称。
    /// </summary>
    public string DefaultPayloadFormatterName { get; set; }

    /// <summary>
    /// 可选 payload 大小上限。为空或小于 0 时不限制。
    /// </summary>
    public long? MaxPayloadSizeBytes { get; set; }
}
