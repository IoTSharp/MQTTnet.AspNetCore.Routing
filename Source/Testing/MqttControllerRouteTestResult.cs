using MQTTnet;
using MQTTnet.Packets;
using MQTTnet.Protocol;
using MQTTnet.Server;
using System.Collections.Generic;

#nullable enable

namespace MQTTnet.AspNetCore.Routing.Testing
{
    /// <summary>
    /// controller route 直接调用的测试结果。
    /// </summary>
    public sealed class MqttControllerRouteTestResult
    {
        internal MqttControllerRouteTestResult(
            InterceptingPublishEventArgs interceptingPublishContext,
            MqttRouteInvocationResult invocationResult)
        {
            InterceptingPublishContext = interceptingPublishContext;
            Match = new MqttRouteTestMatch(
                invocationResult.IsMatched,
                invocationResult.Route,
                MqttRouteContext.ToRouteValues(invocationResult.RouteValues));
            ModelState = invocationResult.ModelState;
        }

        /// <summary>
        /// MQTTnet publish 拦截上下文。
        /// </summary>
        public InterceptingPublishEventArgs InterceptingPublishContext { get; }

        /// <summary>
        /// route 匹配结果。
        /// </summary>
        public MqttRouteTestMatch Match { get; }

        /// <summary>
        /// 绑定与执行过程产生的 model state。
        /// </summary>
        public MqttModelStateDictionary ModelState { get; }

        /// <summary>
        /// 是否匹配到 controller route。
        /// </summary>
        public bool IsMatched => Match.IsMatched;

        /// <summary>
        /// 入站 PUBLISH 是否继续交给 broker 原始投递。
        /// </summary>
        public bool ProcessPublish => InterceptingPublishContext.ProcessPublish;

        /// <summary>
        /// 执行结果是否要求关闭连接。
        /// </summary>
        public bool CloseConnection => InterceptingPublishContext.CloseConnection;

        /// <summary>
        /// PUBACK reason code。
        /// </summary>
        public MqttPubAckReasonCode ReasonCode => InterceptingPublishContext.Response.ReasonCode;

        /// <summary>
        /// PUBACK reason string。
        /// </summary>
        public string? ReasonString => InterceptingPublishContext.Response.ReasonString;

        /// <summary>
        /// PUBACK user properties。
        /// </summary>
        public IReadOnlyList<MqttUserProperty> UserProperties =>
            InterceptingPublishContext.Response.UserProperties ?? (IReadOnlyList<MqttUserProperty>)System.Array.Empty<MqttUserProperty>();

        /// <summary>
        /// 原始 MQTT 应用消息。
        /// </summary>
        public MqttApplicationMessage Message => InterceptingPublishContext.ApplicationMessage;
    }
}
