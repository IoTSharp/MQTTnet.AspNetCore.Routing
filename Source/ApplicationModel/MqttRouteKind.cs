namespace MQTTnet.AspNetCore.Routing
{
    /// <summary>
    /// MQTT route 的编程模型来源。
    /// </summary>
    public enum MqttRouteKind
    {
        /// <summary>
        /// route 来自 controller action attribute routing。
        /// </summary>
        ControllerAction = 0,

        /// <summary>
        /// route 来自 slim application message delegate routing。
        /// </summary>
        ApplicationMessage = 1
    }
}
