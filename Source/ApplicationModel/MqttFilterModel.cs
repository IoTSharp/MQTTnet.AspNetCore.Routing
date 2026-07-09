using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

#nullable enable

namespace MQTTnet.AspNetCore.Routing
{
    /// <summary>
    /// 描述 MQTT filter 元数据。
    /// </summary>
    public sealed class MqttFilterModel
    {
        /// <summary>
        /// 创建基于类型注册的 filter 元数据。
        /// </summary>
        /// <param name="filterType">filter 类型。</param>
        /// <param name="order">filter 排序值。</param>
        /// <param name="metadata">filter 相关自定义元数据。</param>
        public MqttFilterModel(
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type filterType,
            int order = 0,
            IEnumerable<object>? metadata = null)
        {
            FilterType = filterType ?? throw new ArgumentNullException(nameof(filterType));
            FilterInstance = null;
            Order = order;
            Metadata = MqttModelCollection.ToReadOnlyList(metadata);
        }

        /// <summary>
        /// 创建基于实例注册的 filter 元数据。
        /// </summary>
        /// <param name="filterInstance">filter 实例。</param>
        /// <param name="order">filter 排序值。</param>
        /// <param name="metadata">filter 相关自定义元数据。</param>
        public MqttFilterModel(
            IMqttFilterMetadata filterInstance,
            int order = 0,
            IEnumerable<object>? metadata = null)
        {
            FilterType = null;
            FilterInstance = filterInstance ?? throw new ArgumentNullException(nameof(filterInstance));
            Order = order;
            Metadata = MqttModelCollection.ToReadOnlyList(metadata);
        }

        /// <summary>
        /// filter 类型；实例型 filter 可为空。
        /// </summary>
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        public Type? FilterType { get; }

        /// <summary>
        /// filter 实例；类型注册型 filter 可为空。
        /// </summary>
        public IMqttFilterMetadata? FilterInstance { get; }

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
