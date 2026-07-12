// Copyright (c) Atlas Lift Tech Inc. All rights reserved.

using System.Threading;
using System.Threading.Tasks;
using MQTTnet.Server;

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

        /// <summary>
        /// 分发 MQTT server 拦截到的 PUBLISH，并保留 session、PUBACK 与 server 上下文。
        /// </summary>
        Task<MqttApplicationMessageDispatchResult> DispatchAsync(
            InterceptingPublishEventArgs eventArgs,
            MqttServer mqttServer,
            CancellationToken cancellationToken = default);
    }
}
