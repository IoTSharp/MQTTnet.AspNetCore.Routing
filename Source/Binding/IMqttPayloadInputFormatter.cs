using System.Threading.Tasks;

#nullable enable

namespace MQTTnet.AspNetCore.Routing
{
    /// <summary>
    /// MQTT payload 输入 formatter。
    /// </summary>
    public interface IMqttPayloadInputFormatter
    {
        /// <summary>
        /// formatter 名称，用于属性显式选择。
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 判断当前 formatter 是否能读取目标 payload。
        /// </summary>
        /// <param name="context">formatter 上下文。</param>
        bool CanRead(MqttPayloadInputFormatterContext context);

        /// <summary>
        /// 读取 payload 并转换为目标模型。
        /// </summary>
        /// <param name="context">formatter 上下文。</param>
        ValueTask<object?> ReadAsync(MqttPayloadInputFormatterContext context);
    }
}
