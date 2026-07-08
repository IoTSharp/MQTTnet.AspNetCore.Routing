using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

#nullable enable

namespace MQTTnet.AspNetCore.Routing
{
    /// <summary>
    /// 描述 MQTT controller action。
    /// </summary>
    public sealed class MqttActionModel
    {
        internal MqttActionModel(
            Type controllerType,
            MethodInfo actionMethod,
            IEnumerable<MqttRouteModel> routes,
            IEnumerable<MqttParameterModel> parameters,
            Type? payloadType = null,
            Type? resultType = null,
            string? declaredContentType = null,
            string? declaredPayloadFormatterName = null,
            IEnumerable<MqttFilterModel>? filters = null,
            IEnumerable<object>? metadata = null)
        {
            ControllerType = controllerType ?? throw new ArgumentNullException(nameof(controllerType));
            ActionMethod = actionMethod ?? throw new ArgumentNullException(nameof(actionMethod));
            Name = actionMethod.Name;
            Routes = MqttModelCollection.ToReadOnlyList(routes);
            Parameters = MqttModelCollection.ToReadOnlyList(parameters);
            PayloadType = payloadType;
            ResultType = resultType ?? actionMethod.ReturnType;
            DeclaredContentType = declaredContentType;
            DeclaredPayloadFormatterName = declaredPayloadFormatterName;
            Filters = MqttModelCollection.ToReadOnlyList(filters);
            Metadata = MqttModelCollection.ToReadOnlyList(metadata);
        }

        /// <summary>
        /// controller 类型。
        /// </summary>
        public Type ControllerType { get; }

        /// <summary>
        /// action 方法。
        /// </summary>
        public MethodInfo ActionMethod { get; }

        /// <summary>
        /// action 名称。
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// action 暴露的完整 route 模板。
        /// </summary>
        public IReadOnlyList<MqttRouteModel> Routes { get; }

        /// <summary>
        /// action 参数模型。
        /// </summary>
        public IReadOnlyList<MqttParameterModel> Parameters { get; }

        /// <summary>
        /// payload 类型；没有 payload 参数时为空。
        /// </summary>
        public Type? PayloadType { get; }

        /// <summary>
        /// action 返回类型。
        /// </summary>
        public Type ResultType { get; }

        /// <summary>
        /// action 声明的内容类型；当前没有声明时为空。
        /// </summary>
        public string? DeclaredContentType { get; }

        /// <summary>
        /// action 声明的 payload formatter 名称；当前没有声明时为空。
        /// </summary>
        public string? DeclaredPayloadFormatterName { get; }

        /// <summary>
        /// action 关联的 filter 元数据。
        /// </summary>
        public IReadOnlyList<MqttFilterModel> Filters { get; }

        /// <summary>
        /// action 自定义元数据。
        /// </summary>
        public IReadOnlyList<object> Metadata { get; }

        internal static Type? FindPayloadType(IEnumerable<MqttParameterModel> parameters)
        {
            return FindPayloadParameter(parameters)?.ParameterType;
        }

        internal static MqttParameterModel? FindPayloadParameter(IEnumerable<MqttParameterModel> parameters)
        {
            return parameters.FirstOrDefault(p => p.BindingSource == MqttBindingSource.Payload);
        }
    }
}
