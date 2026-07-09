using System;
using System.Collections.Generic;

#nullable enable

namespace MQTTnet.AspNetCore.Routing.Testing
{
    /// <summary>
    /// MQTT route 匹配测试结果。
    /// </summary>
    public sealed class MqttRouteTestMatch
    {
        private static readonly IReadOnlyDictionary<string, object?> EmptyRouteValues =
            new Dictionary<string, object?>(0);

        internal MqttRouteTestMatch(
            bool isMatched,
            MqttRouteModel? route,
            IReadOnlyDictionary<string, object?>? routeValues)
        {
            IsMatched = isMatched;
            Route = route;
            RouteValues = routeValues ?? EmptyRouteValues;
        }

        /// <summary>
        /// 是否匹配到 route。
        /// </summary>
        public bool IsMatched { get; }

        /// <summary>
        /// 匹配到的 route 元数据。
        /// </summary>
        public MqttRouteModel? Route { get; }

        /// <summary>
        /// 匹配到的 route template。
        /// </summary>
        public string? Template => Route?.Template;

        /// <summary>
        /// 匹配得到的 route value。
        /// </summary>
        public IReadOnlyDictionary<string, object?> RouteValues { get; }

        /// <summary>
        /// 读取并转换 route value。
        /// </summary>
        /// <typeparam name="T">目标类型。</typeparam>
        /// <param name="key">route value 名称。</param>
        public T? GetRouteValue<T>(string key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            EnsureMatched();
            if (!RouteValues.TryGetValue(key, out var rawValue))
            {
                throw new MqttRouteTestException($"Route value '{key}' was not found.");
            }

            if (!MqttRouteValueConverter.TryConvert(rawValue, typeof(T), out var convertedValue, out var errorMessage))
            {
                throw new MqttRouteTestException(errorMessage ?? $"Route value '{key}' could not be converted.");
            }

            return (T?)convertedValue;
        }

        /// <summary>
        /// 确认匹配成功；未匹配时抛出测试异常。
        /// </summary>
        /// <param name="message">自定义断言失败说明。</param>
        public void EnsureMatched(string? message = null)
        {
            if (!IsMatched)
            {
                throw new MqttRouteTestException(message ?? "Expected MQTT topic to match a route.");
            }
        }

        /// <summary>
        /// 确认未匹配；匹配成功时抛出测试异常。
        /// </summary>
        /// <param name="message">自定义断言失败说明。</param>
        public void EnsureNotMatched(string? message = null)
        {
            if (IsMatched)
            {
                throw new MqttRouteTestException(message ?? $"Expected MQTT topic not to match a route, but matched '{Template}'.");
            }
        }
    }
}
