using System;
using System.Collections.Generic;

namespace MQTTnet.AspNetCore.Routing
{
    /// <summary>
    /// 保存 MQTT route value、payload 等绑定来源产生的错误。
    /// </summary>
    public sealed class MqttModelStateDictionary
    {
        private static readonly IReadOnlyList<MqttModelStateError> EmptyErrors = Array.Empty<MqttModelStateError>();
        private readonly Dictionary<string, List<MqttModelStateError>> _errors =
            new Dictionary<string, List<MqttModelStateError>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 获取指定 key 的绑定错误。
        /// </summary>
        /// <param name="key">参数名或绑定来源名。</param>
        public IReadOnlyList<MqttModelStateError> this[string key]
        {
            get
            {
                return TryGetErrors(key, out var errors) ? errors : EmptyErrors;
            }
        }

        /// <summary>
        /// 当前是否没有绑定错误。
        /// </summary>
        public bool IsValid => ErrorCount == 0;

        /// <summary>
        /// 绑定错误总数。
        /// </summary>
        public int ErrorCount { get; private set; }

        /// <summary>
        /// 包含错误的 key 集合。
        /// </summary>
        public IEnumerable<string> Keys => _errors.Keys;

        /// <summary>
        /// 添加一个绑定错误。
        /// </summary>
        /// <param name="key">参数名或绑定来源名。</param>
        /// <param name="errorCode">标准错误码。</param>
        /// <param name="message">面向调用方的稳定错误说明。</param>
        public void AddModelError(string key, MqttBindingErrorCode errorCode, string message)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (!_errors.TryGetValue(key, out var errors))
            {
                errors = new List<MqttModelStateError>();
                _errors.Add(key, errors);
            }

            errors.Add(new MqttModelStateError(errorCode, message));
            ErrorCount++;
        }

        /// <summary>
        /// 尝试读取指定 key 的绑定错误。
        /// </summary>
        /// <param name="key">参数名或绑定来源名。</param>
        /// <param name="errors">绑定错误列表。</param>
        public bool TryGetErrors(string key, out IReadOnlyList<MqttModelStateError> errors)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (_errors.TryGetValue(key, out var existingErrors))
            {
                errors = existingErrors;
                return true;
            }

            errors = EmptyErrors;
            return false;
        }
    }
}
