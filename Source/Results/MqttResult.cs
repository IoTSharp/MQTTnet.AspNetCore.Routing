using MQTTnet.Packets;
using MQTTnet.Protocol;
using MQTTnet.Server;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

#nullable enable

namespace MQTTnet.AspNetCore.Routing
{
    /// <summary>
    /// MQTT action 返回结果的基类，负责描述入站 PUBLISH 的处置语义。
    /// </summary>
    public abstract class MqttResult
    {
        private readonly List<MqttUserProperty> _userProperties;

        /// <summary>
        /// 创建 MQTT result。
        /// </summary>
        /// <param name="disposition">当前入站 PUBLISH 的处置方式；为空表示不修改现有处置。</param>
        /// <param name="reasonCode">写入 PUBACK 的 MQTT v5 reason code；为空时使用处置方式默认值。</param>
        /// <param name="reasonString">写入 PUBACK 的 MQTT v5 reason string。</param>
        protected MqttResult(
            MqttInboundPublishDisposition? disposition = null,
            MqttPubAckReasonCode? reasonCode = null,
            string? reasonString = null)
        {
            Disposition = disposition;
            ReasonCode = reasonCode;
            ReasonString = reasonString;
            _userProperties = new List<MqttUserProperty>();
        }

        /// <summary>
        /// 当前入站 PUBLISH 的处置方式；为空表示保持调用前状态。
        /// </summary>
        public MqttInboundPublishDisposition? Disposition { get; }

        /// <summary>
        /// 写入 PUBACK 的 MQTT v5 reason code。
        /// </summary>
        public MqttPubAckReasonCode? ReasonCode { get; }

        /// <summary>
        /// 写入 PUBACK 的 MQTT v5 reason string。
        /// </summary>
        public string? ReasonString { get; }

        /// <summary>
        /// 是否关闭当前 MQTT 连接。
        /// </summary>
        public bool CloseConnection { get; init; }

        /// <summary>
        /// 写入 PUBACK 的 MQTT v5 user properties。
        /// </summary>
        public IList<MqttUserProperty> UserProperties => _userProperties;

        /// <summary>
        /// 执行 result。
        /// </summary>
        /// <param name="context">当前 MQTT action 上下文。</param>
        public virtual ValueTask ExecuteAsync(MqttActionContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            ApplyInboundPublishDisposition(context);
            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// 将 result 的入站 PUBLISH 处置写入 MQTTnet 拦截上下文。
        /// </summary>
        /// <param name="context">当前 MQTT action 上下文。</param>
        protected void ApplyInboundPublishDisposition(MqttActionContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var interceptingPublish = context.InterceptingPublishContext;
            if (interceptingPublish == null)
            {
                return;
            }

            if (Disposition.HasValue)
            {
                interceptingPublish.ProcessPublish = Disposition.Value == MqttInboundPublishDisposition.Continue;
            }

            if (CloseConnection)
            {
                interceptingPublish.CloseConnection = true;
            }

            var response = interceptingPublish.Response;
            var reasonCode = ReasonCode ?? GetDefaultReasonCode(Disposition);
            if (reasonCode.HasValue)
            {
                response.ReasonCode = reasonCode.Value;
            }

            if (!string.IsNullOrEmpty(ReasonString))
            {
                response.ReasonString = ReasonString;
            }

            if (_userProperties.Count > 0)
            {
                response.UserProperties ??= new List<MqttUserProperty>();
                response.UserProperties.AddRange(_userProperties);
            }
        }

        private static MqttPubAckReasonCode? GetDefaultReasonCode(MqttInboundPublishDisposition? disposition)
        {
            return disposition switch
            {
                MqttInboundPublishDisposition.Continue => MqttPubAckReasonCode.Success,
                MqttInboundPublishDisposition.Suppress => MqttPubAckReasonCode.Success,
                MqttInboundPublishDisposition.Reject => MqttPubAckReasonCode.UnspecifiedError,
                _ => null
            };
        }
    }
}
