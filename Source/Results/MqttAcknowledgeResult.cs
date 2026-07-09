using MQTTnet.Protocol;

#nullable enable

namespace MQTTnet.AspNetCore.Routing
{
    /// <summary>
    /// 表示 action 已处理消息，并允许 broker 继续投递原始 PUBLISH。
    /// </summary>
    public sealed class MqttAcknowledgeResult : MqttResult
    {
        /// <summary>
        /// 创建继续投递原始 PUBLISH 的确认结果。
        /// </summary>
        /// <param name="reasonString">可选 MQTT v5 reason string。</param>
        public MqttAcknowledgeResult(string? reasonString = null)
            : base(
                MqttInboundPublishDisposition.Continue,
                MqttPubAckReasonCode.Success,
                reasonString)
        {
        }
    }
}
