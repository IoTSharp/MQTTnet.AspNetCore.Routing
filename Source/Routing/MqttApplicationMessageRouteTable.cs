// Copyright (c) Atlas Lift Tech Inc. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace MQTTnet.AspNetCore.Routing
{
    internal sealed class MqttApplicationMessageRouteTable
    {
        private readonly MqttApplicationMessageRoute[] _firstSegmentFallbackRoutes;
        private readonly Dictionary<string, MqttApplicationMessageRoute[]> _firstLiteralRouteIndex;
        private readonly StringComparer _topicSegmentComparer;

        public MqttApplicationMessageRouteTable(MqttApplicationMessageRoute[] routes)
            : this(routes, StringComparer.OrdinalIgnoreCase)
        {
        }

        public MqttApplicationMessageRouteTable(
            MqttApplicationMessageRoute[] routes,
            StringComparer topicSegmentComparer)
        {
            _topicSegmentComparer = topicSegmentComparer ?? StringComparer.OrdinalIgnoreCase;
            _firstLiteralRouteIndex = BuildFirstLiteralRouteIndex(routes, _topicSegmentComparer, out _firstSegmentFallbackRoutes);
            var applicationModel = new MqttApplicationModel(
                controllers: null,
                routes: routes.Select(route => route.RouteModel).ToArray());
            Catalog = new MqttRouteCatalog(applicationModel);
            Catalog.ThrowIfErrors();
        }

        public MqttRouteCatalog Catalog { get; }

        public bool TryMatch(string topic, out MqttApplicationMessageRoute? route, out IReadOnlyDictionary<string, string> routeValues)
        {
            var topicSegments = MqttApplicationMessageRoute.SplitTopic(topic);
            var candidates = SelectCandidates(topicSegments);
            foreach (var candidate in candidates)
            {
                if (candidate.TryMatch(topicSegments, _topicSegmentComparer, out routeValues))
                {
                    route = candidate;
                    return true;
                }
            }

            route = null;
            routeValues = MqttApplicationMessageRoute.EmptyRouteValues;
            return false;
        }

        private MqttApplicationMessageRoute[] SelectCandidates(string[] topicSegments)
        {
            if (topicSegments.Length > 0 &&
                _firstLiteralRouteIndex.TryGetValue(topicSegments[0], out var indexedRoutes))
            {
                return indexedRoutes;
            }

            return _firstSegmentFallbackRoutes;
        }

        private static Dictionary<string, MqttApplicationMessageRoute[]> BuildFirstLiteralRouteIndex(
            MqttApplicationMessageRoute[] routes,
            StringComparer topicSegmentComparer,
            out MqttApplicationMessageRoute[] fallbackRoutes)
        {
            var literalKeys = new HashSet<string>(topicSegmentComparer);
            var fallback = new List<MqttApplicationMessageRoute>();

            for (var i = 0; i < routes.Length; i++)
            {
                var route = routes[i];
                if (route.HasLiteralFirstSegment(out var literal) && literal != null)
                {
                    literalKeys.Add(literal);
                }
                else
                {
                    fallback.Add(route);
                }
            }

            fallbackRoutes = fallback.ToArray();
            var index = new Dictionary<string, MqttApplicationMessageRoute[]>(literalKeys.Count, topicSegmentComparer);
            foreach (var literalKey in literalKeys)
            {
                var candidates = new List<MqttApplicationMessageRoute>();
                for (var i = 0; i < routes.Length; i++)
                {
                    var route = routes[i];
                    if (!route.HasLiteralFirstSegment(out var firstLiteral) ||
                        topicSegmentComparer.Equals(firstLiteral, literalKey))
                    {
                        candidates.Add(route);
                    }
                }

                index.Add(literalKey, candidates.ToArray());
            }

            return index;
        }
    }
}
