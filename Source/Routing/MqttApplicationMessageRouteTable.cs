// Copyright (c) Atlas Lift Tech Inc. All rights reserved.

using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace MQTTnet.AspNetCore.Routing
{
    internal sealed class MqttApplicationMessageRouteTable
    {
        private readonly MqttApplicationMessageRoute[] _routes;

        public MqttApplicationMessageRouteTable(MqttApplicationMessageRoute[] routes)
        {
            _routes = routes;
            var applicationModel = new MqttApplicationModel(
                controllers: null,
                routes: routes.Select(route => route.RouteModel).ToArray());
            Catalog = new MqttRouteCatalog(applicationModel);
            Catalog.ThrowIfErrors();
        }

        public MqttRouteCatalog Catalog { get; }

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
