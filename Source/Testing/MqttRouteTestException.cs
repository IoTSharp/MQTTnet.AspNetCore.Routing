using System;

#nullable enable

namespace MQTTnet.AspNetCore.Routing.Testing
{
    /// <summary>
    /// MQTT routing 测试辅助 API 抛出的断言异常。
    /// </summary>
    public sealed class MqttRouteTestException : Exception
    {
        /// <summary>
        /// 创建测试断言异常。
        /// </summary>
        /// <param name="message">断言失败说明。</param>
        public MqttRouteTestException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// 创建测试断言异常。
        /// </summary>
        /// <param name="message">断言失败说明。</param>
        /// <param name="innerException">触发断言失败的内部异常。</param>
        public MqttRouteTestException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
