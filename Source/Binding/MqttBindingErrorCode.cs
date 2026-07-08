namespace MQTTnet.AspNetCore.Routing
{
    /// <summary>
    /// MQTT 参数绑定失败的标准错误码。
    /// </summary>
    public enum MqttBindingErrorCode
    {
        /// <summary>
        /// 路由参数未提供。
        /// </summary>
        MissingRouteValue,

        /// <summary>
        /// 路由参数无法转换为目标类型。
        /// </summary>
        TypeConversionFailed,

        /// <summary>
        /// Payload 无法反序列化为目标类型。
        /// </summary>
        PayloadDeserializationFailed
    }
}
