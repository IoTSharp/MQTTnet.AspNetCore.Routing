using MQTTnet.AspNetCore.Routing.Routing;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MQTTnet.AspNetCore.Routing;

public class MqttRoutingOptions

{
    public JsonSerializerOptions SerializerOptions { get;internal set; }
    public Assembly[] FromAssemblies { get; internal set; }
    public JsonSerializerContext SerializerContext { get; internal set; }
    public Type[] ControllerTypes { get; internal set; }
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    public Type RouteInvocationInterceptor { get; internal set; }

 
}
