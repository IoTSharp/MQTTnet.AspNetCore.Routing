using System;
using System.Linq;

#nullable enable

namespace MQTTnet.AspNetCore.Routing.Testing
{
    /// <summary>
    /// 与具体测试框架无关的 route catalog 断言工具。
    /// </summary>
    public static class MqttRouteCatalogAssert
    {
        /// <summary>
        /// 确认 catalog 不包含错误级诊断。
        /// </summary>
        /// <param name="catalog">route catalog。</param>
        public static void HasNoErrors(MqttRouteCatalog catalog)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            if (catalog.HasErrors)
            {
                throw new MqttRouteTestException(catalog.CreateSnapshot());
            }
        }

        /// <summary>
        /// 确认 catalog 包含指定 template 的 route，并返回该 route。
        /// </summary>
        /// <param name="catalog">route catalog。</param>
        /// <param name="template">route template。</param>
        public static MqttRouteModel ContainsRoute(MqttRouteCatalog catalog, string template)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            if (template == null)
            {
                throw new ArgumentNullException(nameof(template));
            }

            var route = catalog.Routes.FirstOrDefault(item => string.Equals(item.Template, template, StringComparison.Ordinal));
            if (route == null)
            {
                throw new MqttRouteTestException($"Expected route catalog to contain route '{template}'.");
            }

            return route;
        }

        /// <summary>
        /// 确认 catalog 不包含指定 template 的 route。
        /// </summary>
        /// <param name="catalog">route catalog。</param>
        /// <param name="template">route template。</param>
        public static void DoesNotContainRoute(MqttRouteCatalog catalog, string template)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            if (template == null)
            {
                throw new ArgumentNullException(nameof(template));
            }

            if (catalog.Routes.Any(item => string.Equals(item.Template, template, StringComparison.Ordinal)))
            {
                throw new MqttRouteTestException($"Expected route catalog not to contain route '{template}'.");
            }
        }

        /// <summary>
        /// 确认 catalog 包含指定 controller action route，并返回该 route。
        /// </summary>
        /// <param name="catalog">route catalog。</param>
        /// <param name="controllerType">controller 类型。</param>
        /// <param name="actionName">action 名称。</param>
        /// <param name="template">可选 route template。</param>
        public static MqttRouteModel ContainsControllerAction(
            MqttRouteCatalog catalog,
            Type controllerType,
            string actionName,
            string? template = null)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            if (controllerType == null)
            {
                throw new ArgumentNullException(nameof(controllerType));
            }

            if (actionName == null)
            {
                throw new ArgumentNullException(nameof(actionName));
            }

            var route = catalog.Routes.FirstOrDefault(item =>
                item.ControllerType == controllerType &&
                item.ActionMethod != null &&
                string.Equals(item.ActionMethod.Name, actionName, StringComparison.Ordinal) &&
                (template == null || string.Equals(item.Template, template, StringComparison.Ordinal)));
            if (route == null)
            {
                var routeHint = template == null ? string.Empty : $" with template '{template}'";
                throw new MqttRouteTestException(
                    $"Expected route catalog to contain action '{controllerType.FullName}.{actionName}'{routeHint}.");
            }

            return route;
        }
    }
}
