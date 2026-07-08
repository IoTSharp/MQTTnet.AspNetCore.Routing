using System;

namespace MQTTnet.AspNetCore.Routing
{
    /// <summary>
    /// 描述一次 MQTT 参数绑定错误。
    /// </summary>
    public sealed class MqttModelStateError
    {
        /// <summary>
        /// 创建绑定错误。
        /// </summary>
        /// <param name="errorCode">标准错误码。</param>
        /// <param name="message">面向调用方的稳定错误说明。</param>
        public MqttModelStateError(MqttBindingErrorCode errorCode, string message)
        {
            ErrorCode = errorCode;
            Message = message ?? throw new ArgumentNullException(nameof(message));
        }

        /// <summary>
        /// 标准错误码。
        /// </summary>
        public MqttBindingErrorCode ErrorCode { get; }

        /// <summary>
        /// 面向调用方的稳定错误说明，不包含反序列化堆栈等调试细节。
        /// </summary>
        public string Message { get; }
    }
}
