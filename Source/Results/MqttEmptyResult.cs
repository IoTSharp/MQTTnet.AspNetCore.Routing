#nullable enable

namespace MQTTnet.AspNetCore.Routing
{
    /// <summary>
    /// 表示不改变当前入站 PUBLISH 处置的空 MQTT result。
    /// </summary>
    internal sealed class MqttEmptyResult : MqttResult
    {
        /// <summary>
        /// 空 result 共享实例。
        /// </summary>
        public static MqttEmptyResult Instance { get; } = new MqttEmptyResult();

        private MqttEmptyResult()
            : base()
        {
        }
    }
}
