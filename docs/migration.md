# Migration

This IoTSharp-maintained fork keeps the original controller-style API and adds a broader MQTT MVC model. Existing applications can migrate in layers.

## Keep Existing Controllers

Existing `[MqttController]`, `[MqttRoute]`, `MqttBaseController`, `Ok()`, and `BadMessage()` usage remains supported.

## Prefer Explicit Controller Registration

Move from assembly scanning:

```csharp
services.AddMqttControllers();
```

To explicit registration:

```csharp
services.AddMqttControllers<MyController>();
```

This improves startup diagnostics and is the recommended trimming path.

## Move Payload Binding To MQTT Attributes

Legacy `[FromPayload]` remains supported. New code can use `[FromMqttPayload]`, `[FromMqttRoute]`, `[FromMqttClient]`, `[FromMqttUserProperty]`, `[FromMqttSession]`, and `[FromMqttContext]`.

## Move Return Values To Results

Legacy controller helpers still work. New action code can return `MqttResult`, `Task<MqttResult>`, or a payload type.

```csharp
public MqttResult Command(CommandPayload payload)
{
    return Suppress();
}
```

## Add Catalog Tests

Use `MqttRouteCatalog` and `MQTTnet.AspNetCore.Routing.Testing` to lock down topic contracts owned by the consuming application.

