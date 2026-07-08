using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

#nullable enable

namespace MQTTnet.AspNetCore.Routing
{
    /// <summary>
    /// 描述当前匹配到的 MQTT route 与 route value。
    /// </summary>
    public sealed class MqttRouteContext
    {
        private static readonly IReadOnlyDictionary<string, object?> EmptyRouteValues =
            new Dictionary<string, object?>(0);

        /// <summary>
        /// 创建 route 上下文。
        /// </summary>
        /// <param name="matchedRoute">匹配到的 route 元数据。</param>
        /// <param name="routeValues">route value 集合。</param>
        public MqttRouteContext(
            MqttRouteModel? matchedRoute,
            IReadOnlyDictionary<string, object?>? routeValues = null)
        {
            MatchedRoute = matchedRoute;
            RouteValues = routeValues ?? EmptyRouteValues;
        }

        /// <summary>
        /// 匹配到的 route 元数据；未匹配或测试构造时可为空。
        /// </summary>
        public MqttRouteModel? MatchedRoute { get; }

        /// <summary>
        /// 与当前 route 关联的 action 方法。
        /// </summary>
        public MethodInfo? ActionMethod => MatchedRoute?.ActionMethod;

        /// <summary>
        /// route value 集合。
        /// </summary>
        public IReadOnlyDictionary<string, object?> RouteValues { get; }

        /// <summary>
        /// 读取并转换 route value。
        /// </summary>
        /// <typeparam name="T">目标类型。</typeparam>
        /// <param name="key">route value 名称。</param>
        public T? GetRouteValue<T>(string key)
        {
            if (!TryGetRouteValue<T>(key, out var value, out var errorMessage))
            {
                throw new KeyNotFoundException(errorMessage ?? $"Route value '{key}' was not found.");
            }

            return value;
        }

        /// <summary>
        /// 尝试读取并转换 route value。
        /// </summary>
        /// <typeparam name="T">目标类型。</typeparam>
        /// <param name="key">route value 名称。</param>
        /// <param name="value">转换后的值。</param>
        /// <param name="errorMessage">失败说明。</param>
        public bool TryGetRouteValue<T>(string key, out T? value, out string? errorMessage)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (!RouteValues.TryGetValue(key, out var rawValue))
            {
                value = default;
                errorMessage = $"Route value '{key}' was not found.";
                return false;
            }

            if (!MqttRouteValueConverter.TryConvert(rawValue, typeof(T), out var convertedValue, out errorMessage))
            {
                value = default;
                return false;
            }

            value = (T?)convertedValue;
            return true;
        }

        internal static IReadOnlyDictionary<string, object?> ToRouteValues(IReadOnlyDictionary<string, object>? values)
        {
            if (values == null || values.Count == 0)
            {
                return EmptyRouteValues;
            }

            return values.ToDictionary(
                pair => pair.Key,
                pair => (object?)pair.Value,
                StringComparer.OrdinalIgnoreCase);
        }

        internal static IReadOnlyDictionary<string, object?> ToRouteValues(IReadOnlyDictionary<string, string>? values)
        {
            if (values == null || values.Count == 0)
            {
                return EmptyRouteValues;
            }

            return values.ToDictionary(
                pair => pair.Key,
                pair => (object?)pair.Value,
                StringComparer.OrdinalIgnoreCase);
        }
    }
}
