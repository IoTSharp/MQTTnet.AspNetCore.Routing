# Source-generated controller routing

`MQTTnet.AspNetCore.Routing.SourceGeneration` is an optional Roslyn analyzer for controller-shaped MQTT handlers that need a Native AOT-friendly invocation path. It belongs entirely to the MQTT routing project and has no dependency on CoAP or its generator.

## What it generates

For controllers marked with `[MqttGeneratedController]`, the generator emits:

- combined class/action topic templates;
- static route registration delegates;
- direct constructor calls with DI service resolution;
- direct action calls and MQTT result execution.

It does not emit a `Type[]` for runtime scanning and does not use `MethodInfo.Invoke` to execute the controller action.

## Current v1 shape

The first version intentionally accepts a narrow signature:

- non-abstract, non-generic controller;
- accessible instance constructor whose parameters come from DI;
- synchronous action returning `MqttResult` or a derived result;
- action parameters are `[FromMqttRoute] string` values.

Unsupported opt-in controllers produce `MQTTGEN001` or `MQTTGEN002` compiler errors instead of silently falling back to reflection.

```csharp
[MqttController]
[MqttGeneratedController]
[MqttRoute("devices")]
internal sealed class DeviceController(DeviceService devices) : MqttBaseController
{
    [MqttRoute("{deviceId}/telemetry")]
    public MqttResult Telemetry([FromMqttRoute] string deviceId)
    {
        devices.Accept(deviceId, Message.Payload);
        return Acknowledge();
    }
}
```

Reference the analyzer and register its generated endpoints:

```xml
<ProjectReference Include="../SourceGeneration/MQTTnet.AspNetCore.Routing.SourceGeneration.csproj"
                  OutputItemType="Analyzer"
                  ReferenceOutputAssembly="false" />
```

```csharp
services.AddMqttApplicationMessageSlimRouting(static routes =>
    MyGeneratedMqttEndpoints.Map(routes));

server.WithApplicationMessageRouting(serviceProvider);
```

Generated routing can run beside attribute routing during migration. A generated dispatcher leaves unmatched messages unchanged, so the existing handler remains authoritative for topics it owns.
