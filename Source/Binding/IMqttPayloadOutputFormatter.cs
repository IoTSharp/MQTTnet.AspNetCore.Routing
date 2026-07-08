using System.Threading.Tasks;

#nullable enable

namespace MQTTnet.AspNetCore.Routing
{
    /// <summary>
    /// MQTT payload 输出 formatter。
    /// </summary>
    public interface IMqttPayloadOutputFormatter
    {
        /// <summary>
        /// formatter 名称，用于显式选择。
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 判断当前 formatter 是否能写出目标 payload。
        /// </summary>
        /// <param name="context">formatter 上下文。</param>
        bool CanWrite(MqttPayloadOutputFormatterContext context);

        /// <summary>
        /// 写出 payload。
        /// </summary>
        /// <param name="context">formatter 上下文。</param>
        ValueTask<MqttPayloadWriteResult> WriteAsync(MqttPayloadOutputFormatterContext context);
    }
}
