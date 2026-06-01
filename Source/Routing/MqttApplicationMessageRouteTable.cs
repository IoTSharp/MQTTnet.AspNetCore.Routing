// Copyright (c) Atlas Lift Tech Inc. All rights reserved.

using System.Collections.Generic;

#nullable enable

namespace MQTTnet.AspNetCore.Routing
{
    internal sealed class MqttApplicationMessageRouteTable
    {
        private readonly MqttApplicationMessageRoute[] _routes;

        public MqttApplicationMessageRouteTable(MqttApplicationMessageRoute[] routes)
        {
            _routes = routes;
        }

        public bool TryMatch(string topic, out MqttApplicationMessageRoute? route, out IReadOnlyDictionary<string, string> routeValues)
        {
            foreach (var candidate in _routes)
            {
                if (candidate.TryMatch(topic, out routeValues))
                {
                    route = candidate;
                    return true;
                }
            }

            route = null;
            routeValues = MqttApplicationMessageRoute.EmptyRouteValues;
            return false;
        }
    }
}
