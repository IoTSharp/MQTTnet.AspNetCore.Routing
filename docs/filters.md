# Filters

Filters provide cross-cutting hooks around routing execution. The pipeline order is authorization, resource, action, exception, and result.

```csharp
public sealed class TenantFilter : IMqttAuthorizationFilter, IOrderedMqttFilter
{
    public int Order => 0;

    public ValueTask OnAuthorizationAsync(MqttAuthorizationFilterContext context)
    {
        if (!context.RequestContext.TryGetUserProperty("tenant", out _))
        {
            context.Result = new MqttRejectResult(MqttPubAckReasonCode.NotAuthorized);
        }

        return ValueTask.CompletedTask;
    }
}
```

Register globally:

```csharp
services.AddMqttControllers<MyController>(options =>
{
    options.AddMqttFilter<TenantFilter>();
});
```

Or add an existing instance:

```csharp
options.AddMqttFilter(new TenantFilter(), order: 0);
```

Built-in filters include payload size limit, model state rejection, exception-to-result conversion, metrics, and logging scope. Business authorization and audit rules should be implemented by the consuming application.

