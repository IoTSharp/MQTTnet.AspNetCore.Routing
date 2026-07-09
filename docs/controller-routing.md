# Controller Routing

Controller routing is the MVC-style path. It discovers `[MqttController]` classes, combines controller and action `[MqttRoute]` templates, binds action parameters, runs filters, and executes MQTT results.

```csharp
[MqttController]
[MqttRoute("devices")]
public sealed class DeviceController : MqttBaseController
{
    [MqttRoute("{deviceId}/telemetry")]
    public MqttResult Telemetry(
        [FromMqttRoute] string deviceId,
        [FromMqttPayload] TelemetryPayload payload)
    {
        return Suppress();
    }
}
```

Register a known controller type when possible:

```csharp
services.AddMqttControllers<DeviceController>(options =>
{
    options.WithJsonSerializerContext(AppJsonContext.Default);
});
```

Assembly scanning remains supported, but it is not the recommended Native AOT path:

```csharp
services.AddMqttControllers(options =>
{
    options.FromAssemblies(typeof(DeviceController).Assembly);
});
```

Attach the router to MQTTnet server publish interception:

```csharp
server.WithAttributeRouting(services, allowUnmatchedRoutes: false);
```

`allowUnmatchedRoutes: false` rejects unmatched publishes at the routing layer. Set it to `true` when the application wants unmatched MQTT messages to continue through broker delivery.

