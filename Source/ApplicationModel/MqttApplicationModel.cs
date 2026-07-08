using System;
using System.Collections.Generic;

#nullable enable

namespace MQTTnet.AspNetCore.Routing
{
    /// <summary>
    /// 描述启动期发现到的 MQTT controller、action 与 route。
    /// </summary>
    public sealed class MqttApplicationModel
    {
        internal MqttApplicationModel(
            IEnumerable<MqttControllerModel>? controllers,
            IEnumerable<MqttRouteModel>? routes,
            IEnumerable<object>? metadata = null)
        {
            Controllers = MqttModelCollection.ToReadOnlyList(controllers);
            Routes = MqttModelCollection.ToReadOnlyList(routes);
            Metadata = MqttModelCollection.ToReadOnlyList(metadata);
        }

        /// <summary>
        /// 空应用模型。
        /// </summary>
        public static MqttApplicationModel Empty { get; } = new MqttApplicationModel(
            Array.Empty<MqttControllerModel>(),
            Array.Empty<MqttRouteModel>());

        /// <summary>
        /// controller 模型集合。
        /// </summary>
        public IReadOnlyList<MqttControllerModel> Controllers { get; }

        /// <summary>
        /// 应用暴露的完整 route 模型集合。
        /// </summary>
        public IReadOnlyList<MqttRouteModel> Routes { get; }

        /// <summary>
        /// 应用级自定义元数据。
        /// </summary>
        public IReadOnlyList<object> Metadata { get; }
    }
}
