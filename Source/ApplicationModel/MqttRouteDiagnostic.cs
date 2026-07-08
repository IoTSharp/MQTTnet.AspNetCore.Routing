using System;
using System.Collections.Generic;

#nullable enable

namespace MQTTnet.AspNetCore.Routing
{
    /// <summary>
    /// route catalog 生成过程中的诊断信息。
    /// </summary>
    public sealed class MqttRouteDiagnostic
    {
        internal MqttRouteDiagnostic(
            MqttRouteDiagnosticSeverity severity,
            string code,
            string message,
            IEnumerable<MqttRouteModel>? routes = null)
        {
            Severity = severity;
            Code = code ?? throw new ArgumentNullException(nameof(code));
            Message = message ?? throw new ArgumentNullException(nameof(message));
            Routes = MqttModelCollection.ToReadOnlyList(routes);
        }

        /// <summary>
        /// 诊断严重程度。
        /// </summary>
        public MqttRouteDiagnosticSeverity Severity { get; }

        /// <summary>
        /// 稳定诊断代码。
        /// </summary>
        public string Code { get; }

        /// <summary>
        /// 诊断说明。
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// 诊断关联的 route。
        /// </summary>
        public IReadOnlyList<MqttRouteModel> Routes { get; }
    }
}
