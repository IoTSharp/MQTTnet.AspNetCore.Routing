namespace MQTTnet.AspNetCore.Routing
{
    /// <summary>
    /// MQTT action 参数的绑定来源。
    /// </summary>
    public enum MqttBindingSource
    {
        /// <summary>
        /// 绑定来源尚未声明或无法从当前模型推断。
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// 参数来自 MQTT topic route value。
        /// </summary>
        Route = 1,

        /// <summary>
        /// 参数来自 MQTT payload。
        /// </summary>
        Payload = 2,

        /// <summary>
        /// 参数来自依赖注入服务。
        /// </summary>
        Services = 3,

        /// <summary>
        /// 参数来自 MQTT 请求或执行上下文。
        /// </summary>
        Context = 4,

        /// <summary>
        /// 参数来自 MQTT server session items。
        /// </summary>
        Session = 5,

        /// <summary>
        /// 参数来自 MQTT client 信息。
        /// </summary>
        Client = 6,

        /// <summary>
        /// 参数来自 MQTT v5 user property。
        /// </summary>
        UserProperty = 7
    }
}
