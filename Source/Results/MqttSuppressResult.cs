using MQTTnet.Protocol;

#nullable enable

namespace MQTTnet.AspNetCore.Routing
{
    /// <summary>
    /// 表示 action 已消费消息，不再让 broker 投递原始 PUBLISH。
    /// </summary>
    public sealed class MqttSuppressResult : MqttResult
    {
        /// <summary>
        /// 创建抑制原始 PUBLISH 投递但返回成功确认的结果。
        /// </summary>
        /// <param name="reasonString">可选 MQTT v5 reason string。</param>
        public MqttSuppressResult(string? reasonString = null)
            : base(
                MqttInboundPublishDisposition.Suppress,
                MqttPubAckReasonCode.Success,
                reasonString)
        {
        }
    }
}
