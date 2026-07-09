using MQTTnet;
using MQTTnet.Server;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using System.Threading;

#nullable enable

namespace MQTTnet.AspNetCore.Routing.Testing
{
    /// <summary>
    /// 用于单元测试的 MQTT request/action 上下文工厂。
    /// </summary>
    public static class MqttTestContexts
    {
        /// <summary>
        /// 创建默认 routing 选项，包含内置 JSON 与 binary formatter。
        /// </summary>
        /// <param name="jsonSerializerOptions">JSON 序列化选项；为空时使用 Web 默认值。</param>
        /// <param name="jsonSerializerContext">source-generated JSON 上下文。</param>
        public static MqttRoutingOptions CreateRoutingOptions(
            JsonSerializerOptions? jsonSerializerOptions = null,
            JsonSerializerContext? jsonSerializerContext = null)
        {
            var options = new MqttRoutingOptions
            {
                SerializerOptions = jsonSerializerOptions ?? CreateDefaultJsonSerializerOptions(),
                SerializerContext = jsonSerializerContext
            };
            options.InputFormatters.Add(new MqttBinaryPayloadInputFormatter());
            options.InputFormatters.Add(new MqttJsonPayloadInputFormatter());
            options.OutputFormatters.Add(new MqttBinaryPayloadOutputFormatter());
            options.OutputFormatters.Add(new MqttJsonPayloadOutputFormatter());
            return options;
        }

        /// <summary>
        /// 创建请求上下文。
        /// </summary>
        /// <param name="message">MQTT 应用消息。</param>
        /// <param name="clientId">客户端标识。</param>
        /// <param name="sessionItems">测试 session items。</param>
        /// <param name="userName">客户端用户名。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        public static MqttRequestContext CreateRequestContext(
            MqttApplicationMessage message,
            string? clientId = "mqtt-test-client",
            IDictionary? sessionItems = null,
            string? userName = null,
            CancellationToken cancellationToken = default)
        {
            return new MqttRequestContext(
                message,
                clientId,
                sessionItems ?? new Hashtable(),
                userName,
                cancellationToken);
        }

        /// <summary>
        /// 创建 action 上下文。
        /// </summary>
        /// <param name="message">MQTT 应用消息。</param>
        /// <param name="clientId">客户端标识。</param>
        /// <param name="routeValues">route value 集合。</param>
        /// <param name="matchedRoute">匹配到的 route 元数据。</param>
        /// <param name="requestServices">请求作用域服务。</param>
        /// <param name="routingOptions">routing 选项；为空时使用默认测试选项。</param>
        /// <param name="modelState">model state；为空时创建空集合。</param>
        /// <param name="sessionItems">测试 session items。</param>
        /// <param name="userName">客户端用户名。</param>
        /// <param name="mqttServer">测试 MQTT server；需要执行 publish result 时传入。</param>
        /// <param name="interceptingPublishContext">publish 拦截上下文。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        public static MqttActionContext CreateActionContext(
            MqttApplicationMessage message,
            string? clientId = "mqtt-test-client",
            IReadOnlyDictionary<string, object?>? routeValues = null,
            MqttRouteModel? matchedRoute = null,
            IServiceProvider? requestServices = null,
            MqttRoutingOptions? routingOptions = null,
            MqttModelStateDictionary? modelState = null,
            IDictionary? sessionItems = null,
            string? userName = null,
            MqttServer? mqttServer = null,
            InterceptingPublishEventArgs? interceptingPublishContext = null,
            CancellationToken cancellationToken = default)
        {
            var requestContext = new MqttRequestContext(
                message,
                clientId,
                sessionItems ?? new Hashtable(),
                userName,
                cancellationToken);
            var routeContext = new MqttRouteContext(matchedRoute, routeValues);
            return new MqttActionContext(
                requestContext,
                routeContext,
                modelState ?? new MqttModelStateDictionary(),
                requestServices ?? EmptyServiceProvider.Instance,
                mqttServer: mqttServer,
                interceptingPublishContext: interceptingPublishContext,
                routingOptions: routingOptions ?? CreateRoutingOptions());
        }

        /// <summary>
        /// 创建 MQTTnet publish 拦截上下文，用于测试 controller route 或 result 执行效果。
        /// </summary>
        /// <param name="message">MQTT 应用消息。</param>
        /// <param name="clientId">客户端标识。</param>
        /// <param name="userName">客户端用户名。</param>
        /// <param name="sessionItems">测试 session items。</param>
        /// <param name="processPublish">初始 ProcessPublish 值。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        public static InterceptingPublishEventArgs CreateInterceptingPublishContext(
            MqttApplicationMessage message,
            string? clientId = "mqtt-test-client",
            string? userName = null,
            IDictionary? sessionItems = null,
            bool processPublish = true,
            CancellationToken cancellationToken = default)
        {
            var context = new InterceptingPublishEventArgs(
                message,
                clientId,
                userName,
                sessionItems ?? new Hashtable(),
                cancellationToken)
            {
                ProcessPublish = processPublish
            };
            return context;
        }

        private static JsonSerializerOptions CreateDefaultJsonSerializerOptions()
        {
            return new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
            };
        }

        private sealed class EmptyServiceProvider : IServiceProvider
        {
            public static readonly EmptyServiceProvider Instance = new EmptyServiceProvider();

            private EmptyServiceProvider()
            {
            }

            public object? GetService(Type serviceType)
            {
                return null;
            }
        }
    }
}
