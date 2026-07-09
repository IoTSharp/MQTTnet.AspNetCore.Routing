// Copyright (c) .NET Foundation. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt
// in the project root for license information.

// Modifications Copyright (c) Atlas Lift Tech Inc. All rights reserved.

using System;
using System.Collections.Generic;

namespace MQTTnet.AspNetCore.Routing
{
    internal class MqttRouteTable
    {
        private readonly MqttRoute[] _firstSegmentFallbackRoutes;
        private readonly Dictionary<string, MqttRoute[]> _firstLiteralRouteIndex;
        private readonly StringComparer _topicSegmentComparer;

        public MqttRouteTable(MqttRoute[] routes)
            : this(routes, MqttRouteCatalog.Empty)
        {
        }

        public MqttRouteTable(MqttRoute[] routes, MqttRouteCatalog catalog)
            : this(routes, catalog, StringComparer.OrdinalIgnoreCase)
        {
        }

        public MqttRouteTable(MqttRoute[] routes, MqttRouteCatalog catalog, StringComparer topicSegmentComparer)
        {
            Routes = routes;
            Catalog = catalog;
            _topicSegmentComparer = topicSegmentComparer ?? StringComparer.OrdinalIgnoreCase;
            _firstLiteralRouteIndex = BuildFirstLiteralRouteIndex(routes, _topicSegmentComparer, out _firstSegmentFallbackRoutes);
        }

        public MqttRoute[] Routes { get; }

        public MqttRouteCatalog Catalog { get; }

        internal void Route(MqttRouteMatchContext routeContext)
        {
            var candidates = SelectCandidates(routeContext);
            for (var i = 0; i < candidates.Length; i++)
            {
                candidates[i].Match(routeContext, _topicSegmentComparer);

                if (routeContext.Handler != null)
                {
                    return;
                }
            }
        }

        private MqttRoute[] SelectCandidates(MqttRouteMatchContext routeContext)
        {
            if (routeContext.Segments.Length > 0 &&
                _firstLiteralRouteIndex.TryGetValue(routeContext.Segments[0], out var indexedRoutes))
            {
                return indexedRoutes;
            }

            return _firstSegmentFallbackRoutes;
        }

        private static Dictionary<string, MqttRoute[]> BuildFirstLiteralRouteIndex(
            MqttRoute[] routes,
            StringComparer topicSegmentComparer,
            out MqttRoute[] fallbackRoutes)
        {
            var literalKeys = new HashSet<string>(topicSegmentComparer);
            var fallback = new List<MqttRoute>();

            for (var i = 0; i < routes.Length; i++)
            {
                var route = routes[i];
                if (route.HasLiteralFirstSegment(out var literal))
                {
                    literalKeys.Add(literal);
                }
                else
                {
                    fallback.Add(route);
                }
            }

            fallbackRoutes = fallback.ToArray();
            var index = new Dictionary<string, MqttRoute[]>(literalKeys.Count, topicSegmentComparer);
            foreach (var literalKey in literalKeys)
            {
                var candidates = new List<MqttRoute>();
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
