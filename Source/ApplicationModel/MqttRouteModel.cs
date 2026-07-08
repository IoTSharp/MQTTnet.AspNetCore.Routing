using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

#nullable enable

namespace MQTTnet.AspNetCore.Routing
{
    /// <summary>
    /// 描述一个可匹配 MQTT topic 的 route。
    /// </summary>
    public sealed class MqttRouteModel
    {
        private const DynamicallyAccessedMemberTypes ControllerMemberTypes =
            DynamicallyAccessedMemberTypes.PublicConstructors |
            DynamicallyAccessedMemberTypes.PublicMethods |
            DynamicallyAccessedMemberTypes.PublicProperties |
            DynamicallyAccessedMemberTypes.NonPublicProperties;

        internal MqttRouteModel(
            string template,
            MqttRouteKind kind,
            IReadOnlyList<MqttRouteSegmentDescriptor> segments,
            IEnumerable<MqttParameterModel>? routeParameters = null,
            [DynamicallyAccessedMembers(ControllerMemberTypes)]
            Type? controllerType = null,
            MethodInfo? actionMethod = null,
            string? controllerTemplate = null,
            Type? payloadType = null,
            Type? resultType = null,
            string? declaredContentType = null,
            string? declaredPayloadFormatterName = null,
            IEnumerable<MqttFilterModel>? filters = null,
            IEnumerable<object>? metadata = null,
            RouteTemplate? parsedTemplate = null,
            RouteTemplate? parsedControllerTemplate = null,
            string[]? unusedRouteParameterNames = null)
        {
            Template = template ?? throw new ArgumentNullException(nameof(template));
            Kind = kind;
            Segments = segments ?? throw new ArgumentNullException(nameof(segments));
            RouteParameters = MqttModelCollection.ToReadOnlyList(routeParameters);
            ControllerType = controllerType;
            ActionMethod = actionMethod;
            ControllerTemplate = controllerTemplate;
            PayloadType = payloadType;
            ResultType = resultType;
            DeclaredContentType = declaredContentType;
            DeclaredPayloadFormatterName = declaredPayloadFormatterName;
            Filters = MqttModelCollection.ToReadOnlyList(filters);
            Metadata = MqttModelCollection.ToReadOnlyList(metadata);
            ParsedTemplate = parsedTemplate;
            ParsedControllerTemplate = parsedControllerTemplate;
            UnusedRouteParameterNames = unusedRouteParameterNames ?? Array.Empty<string>();
        }

        /// <summary>
        /// 完整 route 模板。
        /// </summary>
        public string Template { get; }

        /// <summary>
        /// route 来源。
        /// </summary>
        public MqttRouteKind Kind { get; }

        /// <summary>
        /// controller 级 route 模板；slim route 或无 controller 前缀时为空。
        /// </summary>
        public string? ControllerTemplate { get; }

        /// <summary>
        /// controller 类型；slim route 没有 controller 时为空。
        /// </summary>
        [DynamicallyAccessedMembers(ControllerMemberTypes)]
        public Type? ControllerType { get; }

        /// <summary>
        /// action 或 delegate handler 方法。
        /// </summary>
        public MethodInfo? ActionMethod { get; }

        /// <summary>
        /// route 模板参数。
        /// </summary>
        public IReadOnlyList<MqttParameterModel> RouteParameters { get; }

        /// <summary>
        /// payload 类型；没有 payload 绑定时为空。
        /// </summary>
        public Type? PayloadType { get; }

        /// <summary>
        /// action 或 delegate 的返回类型。
        /// </summary>
        public Type? ResultType { get; }

        /// <summary>
        /// route 声明的内容类型；当前没有声明时为空。
        /// </summary>
        public string? DeclaredContentType { get; }

        /// <summary>
        /// route 声明的 payload formatter 名称；当前没有声明时为空。
        /// </summary>
        public string? DeclaredPayloadFormatterName { get; }

        /// <summary>
        /// route 关联的 filter 元数据。
        /// </summary>
        public IReadOnlyList<MqttFilterModel> Filters { get; }

        /// <summary>
        /// route 自定义元数据。
        /// </summary>
        public IReadOnlyList<object> Metadata { get; }

        internal IReadOnlyList<MqttRouteSegmentDescriptor> Segments { get; }

        internal RouteTemplate? ParsedTemplate { get; }

        internal RouteTemplate? ParsedControllerTemplate { get; }

        internal string[] UnusedRouteParameterNames { get; }
    }
}
