using MQTTnet;
using MQTTnet.Packets;
using MQTTnet.Protocol;
using MQTTnet.Server;
using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

#nullable enable

namespace MQTTnet.AspNetCore.Routing
{
    /// <summary>
    /// 描述一次 MQTT publish 请求可用于绑定的协议上下文。
    /// </summary>
    public sealed class MqttRequestContext
    {
        private static readonly IReadOnlyList<MqttUserProperty> EmptyUserProperties = Array.Empty<MqttUserProperty>();

        /// <summary>
        /// 创建请求上下文。
        /// </summary>
        /// <param name="message">MQTT 应用消息。</param>
        /// <param name="clientId">客户端标识。</param>
        /// <param name="sessionItems">server session items；没有 session 时可为空。</param>
        /// <param name="userName">客户端用户名。</param>
        /// <param name="cancellationToken">请求取消令牌。</param>
        public MqttRequestContext(
            MqttApplicationMessage message,
            string? clientId = null,
            IDictionary? sessionItems = null,
            string? userName = null,
            CancellationToken cancellationToken = default)
        {
            Message = message ?? throw new ArgumentNullException(nameof(message));
            ClientId = clientId;
            UserName = userName;
            SessionItems = sessionItems ?? new Hashtable();
            CancellationToken = cancellationToken;
            UserProperties = message.UserProperties ?? EmptyUserProperties;
        }

        /// <summary>
        /// 原始 MQTT 应用消息。
        /// </summary>
        public MqttApplicationMessage Message { get; }

        /// <summary>
        /// 客户端标识；没有连接上下文时为空。
        /// </summary>
        public string? ClientId { get; }

        /// <summary>
        /// 客户端用户名；当前路径无法提供时为空。
        /// </summary>
        public string? UserName { get; }

        /// <summary>
        /// MQTT topic。
        /// </summary>
        public string Topic => Message.Topic;

        /// <summary>
        /// MQTT payload，保留 <see cref="ReadOnlySequence{T}"/> 以避免无条件复制。
        /// </summary>
        public ReadOnlySequence<byte> Payload => Message.Payload;

        /// <summary>
        /// MQTT QoS 等级。
        /// </summary>
        public MqttQualityOfServiceLevel QualityOfServiceLevel => Message.QualityOfServiceLevel;

        /// <summary>
        /// MQTT retain 标志。
        /// </summary>
        public bool Retain => Message.Retain;

        /// <summary>
        /// MQTT v5 response topic。
        /// </summary>
        public string? ResponseTopic => Message.ResponseTopic;

        /// <summary>
        /// MQTT v5 content type。
        /// </summary>
        public string? ContentType => Message.ContentType;

        /// <summary>
        /// MQTT v5 correlation data。
        /// </summary>
        public byte[]? CorrelationData => Message.CorrelationData;

        /// <summary>
        /// MQTT v5 user properties。
        /// </summary>
        public IReadOnlyList<MqttUserProperty> UserProperties { get; }

        /// <summary>
        /// MQTT server session items。
        /// </summary>
        public IDictionary SessionItems { get; }

        /// <summary>
        /// 请求取消令牌。
        /// </summary>
        public CancellationToken CancellationToken { get; }

        /// <summary>
        /// 从 MQTTnet server publish 拦截上下文创建请求上下文。
        /// </summary>
        /// <param name="args">publish 拦截上下文。</param>
        internal static MqttRequestContext FromInterceptingPublish(InterceptingPublishEventArgs args)
        {
            if (args == null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            return new MqttRequestContext(
                args.ApplicationMessage,
                args.ClientId,
                args.SessionItems,
                args.UserName,
                args.CancellationToken);
        }

        /// <summary>
        /// 尝试读取第一个匹配名称的 user property。
        /// </summary>
        /// <param name="name">user property 名称。</param>
        /// <param name="value">第一个匹配值。</param>
        public bool TryGetUserProperty(string name, out string? value)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            foreach (var property in UserProperties)
            {
                if (string.Equals(property.Name, name, StringComparison.Ordinal))
                {
                    value = property.ReadValueAsString();
                    return true;
                }
            }

            value = null;
            return false;
        }

        /// <summary>
        /// 读取指定名称的全部 user property 值。
        /// </summary>
        /// <param name="name">user property 名称。</param>
        public IReadOnlyList<string> GetUserProperties(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            return UserProperties
                .Where(property => string.Equals(property.Name, name, StringComparison.Ordinal))
                .Select(property => property.ReadValueAsString())
                .ToArray();
        }
    }
}
