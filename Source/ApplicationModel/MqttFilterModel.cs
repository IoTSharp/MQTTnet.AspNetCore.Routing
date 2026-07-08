using System;
using System.Collections.Generic;

#nullable enable

namespace MQTTnet.AspNetCore.Routing
{
    /// <summary>
    /// 描述 MQTT filter 元数据。当前版本先提供模型占位，具体 filter 管线由后续阶段接入。
    /// </summary>
    public sealed class MqttFilterModel
    {
        internal MqttFilterModel(
            Type? filterType = null,
            object? filterInstance = null,
            int order = 0,
            IEnumerable<object>? metadata = null)
        {
            FilterType = filterType;
            FilterInstance = filterInstance;
            Order = order;
            Metadata = MqttModelCollection.ToReadOnlyList(metadata);
        }

        /// <summary>
        /// filter 类型；实例型 filter 可为空。
        /// </summary>
        public Type? FilterType { get; }

        /// <summary>
        /// filter 实例；类型注册型 filter 可为空。
        /// </summary>
        public object? FilterInstance { get; }

        /// <summary>
        /// filter 排序值。
        /// </summary>
        public int Order { get; }

        /// <summary>
        /// filter 相关自定义元数据。
        /// </summary>
        public IReadOnlyList<object> Metadata { get; }
    }
}
