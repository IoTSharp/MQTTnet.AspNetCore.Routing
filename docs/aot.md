# Native AOT

For Native AOT and trimming-sensitive applications, prefer explicit registration and source-generated JSON metadata.

## Recommended Slim Path

```csharp
services.AddMqttApplicationMessageSlimRouting(routes =>
{
    routes.MapJson(
        "devices/{deviceId}/telemetry",
        AppJsonContext.Default.TelemetryPayload,
        static (context, payload) =>
        {
            var deviceId = context.GetRouteValue("deviceId");
            return ValueTask.CompletedTask;
        });
});
```

This path does not scan assemblies and uses `JsonTypeInfo<T>` supplied at registration.

## Controller Path

If you use controller routing, prefer known controller types:

```csharp
services.AddMqttControllers<DeviceController>(options =>
{
    options.WithJsonSerializerContext(AppJsonContext.Default);
});
```

Avoid assembly scanning in Native AOT applications. Assembly scanning APIs are kept for compatibility and are annotated with trimming warnings.

## JSON

Use `JsonSerializerContext`:

```csharp
[JsonSerializable(typeof(TelemetryPayload))]
public sealed partial class AppJsonContext : JsonSerializerContext
{
}
```

Then pass it to routing options or slim route registration.

