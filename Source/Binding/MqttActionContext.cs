using MQTTnet;
using MQTTnet.Server;
using System;

#nullable enable

namespace MQTTnet.AspNetCore.Routing
{
    /// <summary>
    /// 描述 MQTT action 执行期间可用于绑定、过滤和结果处理的上下文。
    /// </summary>
    public sealed class MqttActionContext
    {
        /// <summary>
        /// 创建 action 上下文。
        /// </summary>
        /// <param name="requestContext">请求上下文。</param>
        /// <param name="routeContext">route 上下文。</param>
        /// <param name="modelState">绑定错误集合。</param>
        /// <param name="requestServices">当前请求作用域服务。</param>
        /// <param name="loggerScope">日志作用域。</param>
        /// <param name="mqttServer">MQTT server 实例；非 server 路径可为空。</param>
        public MqttActionContext(
            MqttRequestContext requestContext,
            MqttRouteContext routeContext,
            MqttModelStateDictionary modelState,
            IServiceProvider requestServices,
            IDisposable? loggerScope = null,
            MqttServer? mqttServer = null)
        {
            RequestContext = requestContext ?? throw new ArgumentNullException(nameof(requestContext));
            RouteContext = routeContext ?? throw new ArgumentNullException(nameof(routeContext));
            ModelState = modelState ?? throw new ArgumentNullException(nameof(modelState));
            RequestServices = requestServices ?? throw new ArgumentNullException(nameof(requestServices));
            LoggerScope = loggerScope;
            MqttServer = mqttServer;
        }

        /// <summary>
        /// MQTT 请求上下文。
        /// </summary>
        public MqttRequestContext RequestContext { get; }

        /// <summary>
        /// MQTT route 上下文。
        /// </summary>
        public MqttRouteContext RouteContext { get; }

        /// <summary>
        /// 当前绑定过程产生的错误。
        /// </summary>
        public MqttModelStateDictionary ModelState { get; }

        /// <summary>
        /// 当前请求作用域服务。
        /// </summary>
        public IServiceProvider RequestServices { get; }

        /// <summary>
        /// 当前日志作用域；由执行管线负责释放。
        /// </summary>
        public IDisposable? LoggerScope { get; }

        /// <summary>
        /// MQTT server 实例；客户端或直接分发路径为空。
        /// </summary>
        public MqttServer? MqttServer { get; }

        /// <summary>
        /// 原始 MQTT 应用消息。
        /// </summary>
        public MqttApplicationMessage Message => RequestContext.Message;
    }
}
