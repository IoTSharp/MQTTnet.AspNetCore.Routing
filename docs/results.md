# Results

MQTT results describe what should happen to the inbound PUBLISH and whether an additional response message should be published.

## Inbound Publish Disposition

- `MqttAcknowledgeResult`: action handled the message and broker delivery should continue.
- `MqttSuppressResult`: action consumed the message and broker delivery should stop with a success acknowledgement.
- `MqttRejectResult`: action rejects the publish and writes a MQTT v5 PUBACK reason code.

Controller helpers:

```csharp
return Acknowledge();
return Suppress();
return Reject(MqttPubAckReasonCode.NotAuthorized, "not authorized");
```

## Response Messages

`MqttPublishResult` publishes an explicit `MqttApplicationMessage`.

```csharp
return Publish(new MqttApplicationMessageBuilder()
    .WithTopic("responses/device-1")
    .WithPayload("ok")
    .Build());
```

`MqttPayloadResult<T>` writes a CLR payload through output formatters. If no topic is specified, the request must carry a MQTT v5 response topic.

```csharp
return Payload(new CommandResponse { Accepted = true });
```

Plain return values such as `T`, `Task<T>`, and `ValueTask<T>` are also mapped to payload results.

