// Copyright (c) .NET Foundation. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt
// in the project root for license information.

// Modifications Copyright (c) Atlas Lift Tech Inc. All rights reserved.

namespace MQTTnet.AspNetCore.Routing
{
    internal class MqttRouteTable
    {
        public MqttRouteTable(MqttRoute[] routes)
            : this(routes, MqttRouteCatalog.Empty)
        {
        }

        public MqttRouteTable(MqttRoute[] routes, MqttRouteCatalog catalog)
        {
            Routes = routes;
            Catalog = catalog;
        }

        public MqttRoute[] Routes { get; }

        public MqttRouteCatalog Catalog { get; }

        internal void Route(MqttRouteMatchContext routeContext)
        {
            for (var i = 0; i < Routes.Length; i++)
            {
                Routes[i].Match(routeContext);

                if (routeContext.Handler != null)
                {
                    return;
                }
            }
        }
    }
}
