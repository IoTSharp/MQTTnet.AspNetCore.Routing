using System;
using System.Collections.Generic;
using System.Reflection;

#nullable enable

namespace MQTTnet.AspNetCore.Routing
{
    /// <summary>
    /// 描述 MQTT action 参数或 route 模板参数。
    /// </summary>
    public sealed class MqttParameterModel
    {
        internal MqttParameterModel(
            string name,
            Type parameterType,
            MqttBindingSource bindingSource,
            ParameterInfo? parameterInfo = null,
            bool isOptional = false,
            object? defaultValue = null,
            IEnumerable<string>? routeConstraints = null,
            IEnumerable<object>? metadata = null)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            ParameterType = parameterType ?? throw new ArgumentNullException(nameof(parameterType));
            BindingSource = bindingSource;
            ParameterInfo = parameterInfo;
            IsOptional = isOptional;
            DefaultValue = defaultValue;
            RouteConstraints = MqttModelCollection.ToReadOnlyList(routeConstraints);
            Metadata = MqttModelCollection.ToReadOnlyList(metadata);
        }

        /// <summary>
        /// 参数名称。
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 参数类型；route 模板参数在没有显式类型信息时使用 <see cref="string"/>。
        /// </summary>
        public Type ParameterType { get; }

        /// <summary>
        /// 原始 CLR 参数信息；纯 route 模板参数没有对应 CLR 参数时为空。
        /// </summary>
        public ParameterInfo? ParameterInfo { get; }

        /// <summary>
        /// 参数绑定来源。
        /// </summary>
        public MqttBindingSource BindingSource { get; }

        /// <summary>
        /// 参数是否可选。
        /// </summary>
        public bool IsOptional { get; }

        /// <summary>
        /// 参数默认值；没有默认值时为空。
        /// </summary>
        public object? DefaultValue { get; }

        /// <summary>
        /// route 模板参数声明的约束名称。
        /// </summary>
        public IReadOnlyList<string> RouteConstraints { get; }

        /// <summary>
        /// 参数上的自定义元数据。
        /// </summary>
        public IReadOnlyList<object> Metadata { get; }
    }
}
