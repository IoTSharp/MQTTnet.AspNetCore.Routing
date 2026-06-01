// Copyright (c) Atlas Lift Tech Inc. All rights reserved.

namespace MQTTnet.AspNetCore.Routing
{
    public sealed class MqttApplicationMessageDispatchResult
    {
        public MqttApplicationMessageDispatchResult(bool isHandled)
        {
            IsHandled = isHandled;
        }

        public bool IsHandled { get; }
    }
}
