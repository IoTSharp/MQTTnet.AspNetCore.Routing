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
        PayloadDeserializationFailed,

        /// <summary>
        /// 没有找到可读取 payload 的 formatter。
        /// </summary>
        PayloadFormatterNotFound,

        /// <summary>
        /// session item 未提供。
        /// </summary>
        MissingSessionItem,

        /// <summary>
        /// MQTT client 信息未提供。
        /// </summary>
        MissingClientValue,

        /// <summary>
        /// MQTT user property 未提供。
        /// </summary>
        MissingUserProperty,

        /// <summary>
        /// 当前参数类型不能从声明的 binding source 绑定。
        /// </summary>
        UnsupportedBindingSource
    }
}
