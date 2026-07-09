using MQTTnet.Protocol;

#nullable enable

namespace MQTTnet.AspNetCore.Routing
{
    /// <summary>
    /// 表示 action 拒绝当前 PUBLISH。
    /// </summary>
    public sealed class MqttRejectResult : MqttResult
    {
        /// <summary>
        /// 创建拒绝结果。
        /// </summary>
        /// <param name="reasonCode">MQTT v5 PUBACK reason code。</param>
        /// <param name="reasonString">可选 MQTT v5 reason string。</param>
        public MqttRejectResult(
            MqttPubAckReasonCode reasonCode = MqttPubAckReasonCode.UnspecifiedError,
            string? reasonString = null)
            : base(
                MqttInboundPublishDisposition.Reject,
                reasonCode,
                reasonString)
        {
        }
    }
}
