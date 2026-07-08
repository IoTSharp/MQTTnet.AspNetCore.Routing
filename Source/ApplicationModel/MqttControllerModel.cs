using System;
using System.Collections.Generic;

#nullable enable

namespace MQTTnet.AspNetCore.Routing
{
    /// <summary>
    /// 描述 MQTT controller。
    /// </summary>
    public sealed class MqttControllerModel
    {
        internal MqttControllerModel(
            Type controllerType,
            IEnumerable<MqttRouteModel> routes,
            IEnumerable<MqttActionModel> actions,
            IEnumerable<MqttFilterModel>? filters = null,
            IEnumerable<object>? metadata = null)
        {
            ControllerType = controllerType ?? throw new ArgumentNullException(nameof(controllerType));
            Name = controllerType.Name.EndsWith("Controller", StringComparison.Ordinal)
                ? controllerType.Name.Substring(0, controllerType.Name.Length - 10)
                : controllerType.Name;
            Routes = MqttModelCollection.ToReadOnlyList(routes);
            Actions = MqttModelCollection.ToReadOnlyList(actions);
            Filters = MqttModelCollection.ToReadOnlyList(filters);
            Metadata = MqttModelCollection.ToReadOnlyList(metadata);
        }

        /// <summary>
        /// controller 类型。
        /// </summary>
        public Type ControllerType { get; }

        /// <summary>
        /// controller 名称，默认去除 Controller 后缀。
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// controller 级 route 模板。
        /// </summary>
        public IReadOnlyList<MqttRouteModel> Routes { get; }

        /// <summary>
        /// controller 下的 action 模型。
        /// </summary>
        public IReadOnlyList<MqttActionModel> Actions { get; }

        /// <summary>
        /// controller 关联的 filter 元数据。
        /// </summary>
        public IReadOnlyList<MqttFilterModel> Filters { get; }

        /// <summary>
        /// controller 自定义元数据。
        /// </summary>
        public IReadOnlyList<object> Metadata { get; }
    }
}
