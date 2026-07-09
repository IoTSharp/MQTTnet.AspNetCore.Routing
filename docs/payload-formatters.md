# Payload Formatters

Payload formatters convert MQTT payload bytes into action parameters and convert action return values into response payloads. The built-in formatters are JSON and binary.

## Input Formatter

```csharp
public sealed class CustomInputFormatter : IMqttPayloadInputFormatter
{
    public string Name => "custom";

    public bool CanRead(MqttPayloadInputFormatterContext context)
    {
        return context.FormatterName == Name;
    }

    public ValueTask<object?> ReadAsync(MqttPayloadInputFormatterContext context)
    {
        // Decode context.Payload and return context.ModelType.
        return ValueTask.FromResult<object?>(null);
    }
}
```

Register it in routing options:

```csharp
services.AddMqttControllers<MyController>(options =>
{
    options.InputFormatters.Add(new CustomInputFormatter());
});
```

## Output Formatter

`MqttPayloadResult<T>` and plain action return values use `IMqttPayloadOutputFormatter`.

```csharp
services.AddMqttControllers<MyController>(options =>
{
    options.WithDefaultPayloadFormatter("json");
    options.WithDefaultPayloadContentType("application/json");
});
```

For Native AOT, prefer source-generated `JsonSerializerContext` so JSON formatter lookup does not depend on runtime reflection.
