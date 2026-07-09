// Copyright (c) Atlas Lift Tech Inc. All rights reserved.

using Microsoft.AspNetCore.Mvc;
using MQTTnet.AspNetCore.Routing.Attributes;
using MQTTnet.Protocol;
using MQTTnet.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MQTTnet.AspNetCore.Routing
{
    [MqttController]
    public abstract class MqttBaseController
    {
        /// <summary>
        /// Connection context is set by controller activator. If this class is instantiated directly, it will be null.
        /// </summary>
        public InterceptingPublishEventArgs MqttContext => ControllerContext.MqttContext;

        /// <summary>
        /// Gets the <see cref="MqttApplicationMessage"/> for the executing action.
        /// </summary>
        public MqttApplicationMessage Message => MqttContext.ApplicationMessage;

        /// <summary>
        /// Gets the <see cref="MqttServer"/> for the executing action.
        /// </summary>
        public MqttServer Server => ControllerContext.MqttServer;

        public string ClientId => MqttContext.ClientId;
        public async Task<MqttClientStatus> GetClientStatusAsync()
        {
            var clients = await Server.GetClientsAsync();
            return clients.FirstOrDefault(c => c.Id == MqttContext.ClientId);
        }
        public async Task<MqttSessionStatus> GetSessionAsync()
        {
            var client = await GetClientStatusAsync();
            return client?.Session;
        }

        public async Task<T> GetSessionDataAsync<T>(string key)
        {
            var client = await GetClientStatusAsync();
            return (T)(client?.Session.Items[key]);
        }

        /// <summary>
        /// ControllerContext is set by controller activator. If this class is instantiated directly, it will be null.
        /// </summary>
        [MqttControllerContext]
        public MqttControllerContext ControllerContext { get; set; }

        /// <summary>
        /// Create a result that accepts the given message and publishes it to all subscribers on the topic.
        /// </summary>
        /// <returns>The created <see cref="Task"/> for the reponse.</returns>
        [NonAction]
        public virtual Task Ok()
        {
            MqttContext.ProcessPublish = true;
            return Task.CompletedTask;
        }

        /// <summary>
        /// Create a result that accepts the given message and publishes it to all subscribers on the topic. This is an
        /// alias for the <see cref="Ok"/> result.
        /// </summary>
        /// <returns>The created <see cref="Task"/> for the reponse.</returns>
        [NonAction]
        public virtual Task Accepted() => Ok();

        /// <summary>
        /// Create a result that rejects the given message and prevents publishing it to any subscribers.
        /// </summary>
        /// <returns>The created <see cref="Task"/> for the reponse.</returns>
        [NonAction]
        public virtual Task BadMessage()
        {
            MqttContext.ProcessPublish = false;
            return Task.CompletedTask;
        }

        /// <summary>
        /// 返回 MQTT 语义的确认结果，并继续投递原始 PUBLISH。
        /// </summary>
        /// <param name="reasonString">可选 MQTT v5 reason string。</param>
        /// <returns>MQTT 确认结果。</returns>
        [NonAction]
        public virtual MqttAcknowledgeResult Acknowledge(string reasonString = null)
        {
            return new MqttAcknowledgeResult(reasonString);
        }

        /// <summary>
        /// 返回 MQTT 语义的消费结果，不再投递原始 PUBLISH。
        /// </summary>
        /// <param name="reasonString">可选 MQTT v5 reason string。</param>
        /// <returns>MQTT 消费结果。</returns>
        [NonAction]
        public virtual MqttSuppressResult Suppress(string reasonString = null)
        {
            return new MqttSuppressResult(reasonString);
        }

        /// <summary>
        /// 返回 MQTT 语义的拒绝结果。
        /// </summary>
        /// <param name="reasonCode">MQTT v5 PUBACK reason code。</param>
        /// <param name="reasonString">可选 MQTT v5 reason string。</param>
        /// <returns>MQTT 拒绝结果。</returns>
        [NonAction]
        public virtual MqttRejectResult Reject(
            MqttPubAckReasonCode reasonCode = MqttPubAckReasonCode.UnspecifiedError,
            string reasonString = null)
        {
            return new MqttRejectResult(reasonCode, reasonString);
        }

        /// <summary>
        /// 返回向 broker 注入 MQTT 应用消息的结果。
        /// </summary>
        /// <param name="message">要注入 broker 的应用消息。</param>
        /// <param name="disposition">当前入站 PUBLISH 的处置方式；为空表示保持调用前状态。</param>
        /// <returns>MQTT 发布结果。</returns>
        [NonAction]
        public virtual MqttPublishResult Publish(
            MqttApplicationMessage message,
            MqttInboundPublishDisposition? disposition = null)
        {
            return new MqttPublishResult(message, disposition);
        }

        /// <summary>
        /// 返回将 payload 写出到指定 topic 或请求 response topic 的结果。
        /// </summary>
        /// <typeparam name="TPayload">payload 类型。</typeparam>
        /// <param name="payload">要写出的 payload。</param>
        /// <param name="topic">响应 topic；为空时使用请求消息的 response topic。</param>
        /// <param name="disposition">当前入站 PUBLISH 的处置方式；为空表示保持调用前状态。</param>
        /// <returns>MQTT payload 发布结果。</returns>
        [NonAction]
        public virtual MqttPayloadResult<TPayload> Payload<TPayload>(
            TPayload payload,
            string topic = null,
            MqttInboundPublishDisposition? disposition = null)
        {
            return new MqttPayloadResult<TPayload>(payload, topic, disposition);
        }

        public T GetSessionItem<T>(string key)
        {
            return (T)MqttContext.SessionItems[key] ;
        }

        public T GetSessionItem<T>()
        {
            return (T)MqttContext.SessionItems[typeof(T).Name];
        }
    }
}
