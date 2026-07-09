using MQTTnet;
using MQTTnet.Server;
using System;
using System.Collections;
using System.Threading.Tasks;

#nullable enable

namespace MQTTnet.AspNetCore.Routing
{
    /// <summary>
    /// 表示 action 需要额外向 broker 注入一条 MQTT 应用消息。
    /// </summary>
    public class MqttPublishResult : MqttResult
    {
        /// <summary>
        /// 创建发布结果。
        /// </summary>
        /// <param name="message">要注入 broker 的应用消息。</param>
        /// <param name="disposition">当前入站 PUBLISH 的处置方式；为空表示保持调用前状态。</param>
        public MqttPublishResult(
            MqttApplicationMessage message,
            MqttInboundPublishDisposition? disposition = null)
            : base(disposition)
        {
            Message = message ?? throw new ArgumentNullException(nameof(message));
        }

        /// <summary>
        /// 要注入 broker 的应用消息。
        /// </summary>
        public MqttApplicationMessage Message { get; }

        /// <inheritdoc />
        public override async ValueTask ExecuteAsync(MqttActionContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            ApplyInboundPublishDisposition(context);

            if (context.MqttServer == null)
            {
                throw new InvalidOperationException("MqttPublishResult requires an MQTT server to publish the response message.");
            }

            var injectedMessage = new InjectedMqttApplicationMessage(Message)
            {
                CustomSessionItems = new Hashtable { [MqttRoutingInternal.ResultPublishSessionItemKey] = true },
                SenderClientId = "mqtt-routing",
                SenderUserName = "mqtt-routing"
            };
            await context.MqttServer
                .InjectApplicationMessage(injectedMessage, context.RequestContext.CancellationToken)
                .ConfigureAwait(false);
        }
    }
}
