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
            IEnumerable<object>? metadata = null,
            string? bindingName = null,
            string? declaredContentType = null,
            string? formatterName = null)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            ParameterType = parameterType ?? throw new ArgumentNullException(nameof(parameterType));
            BindingSource = bindingSource;
            ParameterInfo = parameterInfo;
            IsOptional = isOptional;
            DefaultValue = defaultValue;
            RouteConstraints = MqttModelCollection.ToReadOnlyList(routeConstraints);
            Metadata = MqttModelCollection.ToReadOnlyList(metadata);
            BindingName = bindingName;
            DeclaredContentType = declaredContentType;
            FormatterName = formatterName;
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
        /// 参数绑定使用的外部名称，例如 route value、session key、client 字段或 user property 名称。
        /// </summary>
        public string? BindingName { get; }

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
        /// payload 参数声明的 content type；非 payload 参数为空。
        /// </summary>
        public string? DeclaredContentType { get; }

        /// <summary>
        /// payload 参数声明的 formatter 名称；非 payload 参数为空。
        /// </summary>
        public string? FormatterName { get; }

        /// <summary>
        /// 参数上的自定义元数据。
        /// </summary>
        public IReadOnlyList<object> Metadata { get; }
    }
}
