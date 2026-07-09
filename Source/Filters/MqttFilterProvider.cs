using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

#nullable enable

namespace MQTTnet.AspNetCore.Routing
{
    /// <summary>
    /// 从全局配置与匹配 route 元数据解析当前 action 的 filter 列表。
    /// </summary>
    internal sealed class MqttFilterProvider
    {
        private readonly MqttRoutingOptions _options;

        /// <summary>
        /// 创建 MQTT filter provider。
        /// </summary>
        /// <param name="options">MQTT routing 配置。</param>
        public MqttFilterProvider(MqttRoutingOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        /// <summary>
        /// 获取当前 action 应执行的 filter 实例，并按 order 和注册顺序排序。
        /// </summary>
        /// <param name="actionContext">当前 MQTT action 上下文。</param>
        /// <param name="serviceProvider">当前请求作用域服务。</param>
        public IReadOnlyList<IMqttFilterMetadata> GetFilters(
            MqttActionContext actionContext,
            IServiceProvider serviceProvider)
        {
            if (actionContext == null)
            {
                throw new ArgumentNullException(nameof(actionContext));
            }

            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            var entries = new List<FilterEntry>();
            AddFilterModels(entries, _options.Filters, serviceProvider);
            AddFilterModels(entries, actionContext.RouteContext.MatchedRoute?.Filters, serviceProvider);

            return entries
                .OrderBy(entry => entry.Order)
                .ThenBy(entry => entry.Index)
                .Select(entry => entry.Filter)
                .ToArray();
        }

        private static void AddFilterModels(
            ICollection<FilterEntry> entries,
            IEnumerable<MqttFilterModel>? models,
            IServiceProvider serviceProvider)
        {
            if (models == null)
            {
                return;
            }

            foreach (var model in models)
            {
                var filter = ResolveFilter(model, serviceProvider);
                var order = filter is IOrderedMqttFilter orderedFilter
                    ? orderedFilter.Order
                    : model.Order;
                entries.Add(new FilterEntry(filter, order, entries.Count));
            }
        }

        private static IMqttFilterMetadata ResolveFilter(
            MqttFilterModel model,
            IServiceProvider serviceProvider)
        {
            if (model.FilterInstance is IMqttFilterMetadata instance)
            {
                return instance;
            }

            var filterType = model.FilterType
                ?? throw new InvalidOperationException("MQTT filter model must define a filter type or instance.");
            if (!typeof(IMqttFilterMetadata).IsAssignableFrom(filterType))
            {
                throw new InvalidOperationException($"MQTT filter type '{filterType.FullName}' must implement {nameof(IMqttFilterMetadata)}.");
            }

            return ResolveFilterType(filterType, serviceProvider);
        }

        private static IMqttFilterMetadata ResolveFilterType(
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type filterType,
            IServiceProvider serviceProvider)
        {
            return (IMqttFilterMetadata)(serviceProvider.GetService(filterType)
                ?? ActivatorUtilities.CreateInstance(serviceProvider, filterType));
        }

        private readonly struct FilterEntry
        {
            public FilterEntry(IMqttFilterMetadata filter, int order, int index)
            {
                Filter = filter;
                Order = order;
                Index = index;
            }

            public IMqttFilterMetadata Filter { get; }

            public int Order { get; }

            public int Index { get; }
        }
    }
}
