# Route Catalog And Testing

`MqttRouteCatalog` is generated during startup for controller routing and slim routing. It exposes route templates, handler metadata, parameter binding sources, payload types, result types, filters, and diagnostics.

```csharp
var catalog = services.GetRequiredService<MqttRouteCatalog>();
catalog.ThrowIfErrors();
var snapshot = catalog.CreateSnapshot();
```

## Testing Namespace

The `MQTTnet.AspNetCore.Routing.Testing` namespace is framework-neutral. It throws `MqttRouteTestException`, so it works with MSTest, xUnit, NUnit, or custom test runners.

```csharp
using MQTTnet.AspNetCore.Routing.Testing;

using var host = MqttRoutingTestHost.Create(services =>
{
    services.AddSingleton(recorder);
    services.AddMqttControllers<DeviceController>(options =>
    {
        options.WithJsonSerializerContext(AppJsonContext.Default);
    });
});

MqttRouteCatalogAssert.ContainsControllerAction(
    host.Catalog,
    typeof(DeviceController),
    nameof(DeviceController.Telemetry),
    "devices/{deviceId}/telemetry");

var match = host.Match("devices/device-1/telemetry");
match.EnsureMatched();
var deviceId = match.GetRouteValue<string>("deviceId");
```

## Direct Controller Invocation

You can invoke controller routing without starting a broker:

```csharp
var result = await host.InvokeControllerAsync(
    MqttTestMessages.CreateJson(
        "devices/device-1/telemetry",
        new TelemetryPayload { Value = 10 },
        AppJsonContext.Default.TelemetryPayload),
    clientId: "test-client");

Assert.IsTrue(result.IsMatched);
Assert.IsFalse(result.ProcessPublish);
Assert.IsTrue(result.ModelState.IsValid);
```

## Slim Route Dispatch

```csharp
var dispatch = await host.DispatchApplicationMessageAsync(
    MqttTestMessages.Create("devices/device-1/ping"),
    clientId: "test-client");
```

## Result Assertions

```csharp
var mqttResult = await MqttTestResults.CreateResultAsync(
    typeof(Task<MqttResult>),
    Task.FromResult<MqttResult>(new MqttRejectResult(MqttPubAckReasonCode.NotAuthorized)));

MqttResultAssert.IsReject(mqttResult, MqttPubAckReasonCode.NotAuthorized);
```

The test helper API intentionally does not encode any business topic policy.

