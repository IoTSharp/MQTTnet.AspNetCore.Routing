# Binding

Binding reads values from MQTT route values, payload, client information, user properties, session items, and routing contexts. Binding failures are recorded in `MqttModelStateDictionary`.

## Sources

```csharp
[MqttRoute("devices/{deviceId}/state")]
public MqttResult State(
    [FromMqttRoute] Guid deviceId,
    [FromMqttPayload] StatePayload payload,
    [FromMqttClient] string clientId,
    [FromMqttUserProperty("trace-id")] string traceId,
    [FromMqttSession("tenant")] string tenant,
    MqttRequestContext request,
    MqttActionContext action)
{
    return Acknowledge();
}
```

Supported route conversions include `string`, `Guid`, numeric types, `bool`, enum, nullable values, and optional route segments.

## Payloads

`[FromMqttPayload]` uses the formatter pipeline. Formatter selection checks the attribute declaration, MQTT v5 content type, route metadata, and default routing options.

```csharp
public MqttResult Command(
    [FromMqttPayload("application/json", FormatterName = "json")] CommandPayload payload)
{
    return Suppress();
}
```

Raw payload values can be bound as `byte[]`, `ReadOnlyMemory<byte>`, or `MqttApplicationMessage`.

