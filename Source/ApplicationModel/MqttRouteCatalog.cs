using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

#nullable enable

namespace MQTTnet.AspNetCore.Routing
{
    /// <summary>
    /// 启动期生成的 MQTT route catalog，可用于测试、诊断和文档导出。
    /// </summary>
    public sealed class MqttRouteCatalog
    {
        internal MqttRouteCatalog(
            MqttApplicationModel applicationModel,
            IEnumerable<MqttRouteModel>? routes = null,
            IEnumerable<MqttRouteDiagnostic>? diagnostics = null)
        {
            ApplicationModel = applicationModel ?? throw new ArgumentNullException(nameof(applicationModel));
            Routes = MqttModelCollection.ToReadOnlyList(routes ?? applicationModel.Routes);
            Diagnostics = MqttModelCollection.ToReadOnlyList(
                diagnostics ?? MqttRouteCatalogDiagnostics.CreateDiagnostics(Routes));
        }

        /// <summary>
        /// 空 route catalog。
        /// </summary>
        public static MqttRouteCatalog Empty { get; } = new MqttRouteCatalog(MqttApplicationModel.Empty);

        /// <summary>
        /// 原始 application model。
        /// </summary>
        public MqttApplicationModel ApplicationModel { get; }

        /// <summary>
        /// 按运行时匹配顺序导出的 route。
        /// </summary>
        public IReadOnlyList<MqttRouteModel> Routes { get; }

        /// <summary>
        /// route catalog 诊断。
        /// </summary>
        public IReadOnlyList<MqttRouteDiagnostic> Diagnostics { get; }

        /// <summary>
        /// 是否存在错误级诊断。
        /// </summary>
        public bool HasErrors => Diagnostics.Any(diagnostic => diagnostic.Severity == MqttRouteDiagnosticSeverity.Error);

        /// <summary>
        /// 如果存在错误级诊断，则抛出启动期异常。
        /// </summary>
        public void ThrowIfErrors()
        {
            if (!HasErrors)
            {
                return;
            }

            var builder = new StringBuilder("MQTT route catalog contains error diagnostics:");
            foreach (var diagnostic in Diagnostics.Where(d => d.Severity == MqttRouteDiagnosticSeverity.Error))
            {
                builder.AppendLine();
                builder.Append(diagnostic.Code);
                builder.Append(": ");
                builder.Append(diagnostic.Message);
            }

            throw new InvalidOperationException(builder.ToString());
        }

        /// <summary>
        /// 生成稳定的 route catalog 文本快照，便于测试断言或人工检查。
        /// </summary>
        /// <returns>按匹配顺序排列的 route 快照。</returns>
        public string CreateSnapshot()
        {
            var builder = new StringBuilder();
            foreach (var route in Routes)
            {
                builder.Append(route.Kind);
                builder.Append(' ');
                builder.Append(route.Template);
                builder.Append(" -> ");
                AppendHandler(builder, route);

                if (route.RouteParameters.Count > 0)
                {
                    builder.Append(" route[");
                    AppendParameters(builder, route.RouteParameters);
                    builder.Append(']');
                }

                if (route.PayloadType != null)
                {
                    builder.Append(" payload=");
                    builder.Append(GetFriendlyName(route.PayloadType));
                }

                if (route.ResultType != null)
                {
                    builder.Append(" result=");
                    builder.Append(GetFriendlyName(route.ResultType));
                }

                builder.AppendLine();
            }

            return builder.ToString();
        }

        private static void AppendHandler(StringBuilder builder, MqttRouteModel route)
        {
            if (route.ControllerType != null && route.ActionMethod != null)
            {
                builder.Append(route.ControllerType.FullName);
                builder.Append('.');
                builder.Append(route.ActionMethod.Name);
                return;
            }

            if (route.ActionMethod != null)
            {
                builder.Append(route.ActionMethod.DeclaringType?.FullName ?? "<delegate>");
                builder.Append('.');
                builder.Append(route.ActionMethod.Name);
                return;
            }

            builder.Append("<unknown>");
        }

        private static void AppendParameters(StringBuilder builder, IReadOnlyList<MqttParameterModel> parameters)
        {
            for (var i = 0; i < parameters.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }

                var parameter = parameters[i];
                builder.Append(parameter.Name);
                builder.Append(':');
                builder.Append(GetFriendlyName(parameter.ParameterType));
                builder.Append(' ');
                builder.Append(parameter.BindingSource);

                if (parameter.RouteConstraints.Count > 0)
                {
                    builder.Append(" constraints=");
                    builder.Append(string.Join("|", parameter.RouteConstraints));
                }
            }
        }

        private static string GetFriendlyName(Type type)
        {
            if (!type.IsGenericType)
            {
                return type.Name;
            }

            var typeName = type.Name;
            var tickIndex = typeName.IndexOf('`');
            if (tickIndex >= 0)
            {
                typeName = typeName.Substring(0, tickIndex);
            }

            return typeName + "<" + string.Join(",", type.GetGenericArguments().Select(GetFriendlyName)) + ">";
        }
    }
}
