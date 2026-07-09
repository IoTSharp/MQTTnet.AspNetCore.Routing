# Performance

The routing library separates the high-throughput slim path from the MVC-style controller path.

## Slim Routing

Use slim routing for hot paths and Native AOT:

- explicit route registration
- no controller discovery
- source-generated `JsonTypeInfo<T>`
- direct delegate dispatch

```csharp
services.AddMqttApplicationMessageSlimRouting(routes =>
{
    routes.MapJson("devices/{deviceId}/telemetry", AppJsonContext.Default.TelemetryPayload, Handler);
});
```

## Controller Routing

Controller routing is optimized around startup metadata and cached routing tables, but it still offers a richer MVC surface: filters, controller activation, action parameters, results, and route catalog diagnostics.

Use these practices:

- Register known controller types instead of scanning assemblies.
- Prefer `JsonSerializerContext` over reflection-based JSON metadata.
- Keep route templates specific; catch-all routes should be last-resort handlers.
- Set `WithCaseSensitiveTopicMatching(true)` only when the application requires MQTT literal case sensitivity.
- Use payload size limits for defensive rejection before action execution.
- Use route catalog snapshot tests to catch accidental route expansion.

## Measurement

The built-in metrics filter records generic routing metrics. Business labels should be added by consuming applications through their own filters.

