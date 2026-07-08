using System;

#nullable enable

namespace MQTTnet.AspNetCore.Routing
{
    /// <summary>
    /// MQTT 参数绑定失败时抛出的异常。
    /// </summary>
    public sealed class MqttBindingException : Exception
    {
        /// <summary>
        /// 创建绑定异常。
        /// </summary>
        /// <param name="modelState">绑定错误集合。</param>
        /// <param name="message">稳定错误说明。</param>
        /// <param name="innerException">内部异常，仅用于日志和诊断。</param>
        public MqttBindingException(
            MqttModelStateDictionary modelState,
            string message,
            Exception? innerException = null)
            : base(message, innerException)
        {
            ModelState = modelState ?? throw new ArgumentNullException(nameof(modelState));
        }

        /// <summary>
        /// 绑定错误集合。
        /// </summary>
        public MqttModelStateDictionary ModelState { get; }
    }
}
