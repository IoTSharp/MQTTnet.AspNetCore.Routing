// Copyright (c) Atlas Lift Tech Inc. All rights reserved.

using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace MQTTnet.AspNetCore.Routing
{
    public interface IMqttApplicationMessageDispatcher
    {
        Task<MqttApplicationMessageDispatchResult> DispatchAsync(
            MqttApplicationMessage applicationMessage,
            string? clientId = null,
            CancellationToken cancellationToken = default);

        Task<MqttApplicationMessageDispatchResult> DispatchAsync(
            MqttApplicationMessageReceivedEventArgs eventArgs,
            CancellationToken cancellationToken = default);
    }
}
