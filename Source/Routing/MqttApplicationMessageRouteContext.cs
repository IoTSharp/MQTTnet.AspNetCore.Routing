// Copyright (c) Atlas Lift Tech Inc. All rights reserved.

using MQTTnet;
using System;
using System.Collections.Generic;
using System.Threading;

#nullable enable

namespace MQTTnet.AspNetCore.Routing
{
    public sealed class MqttApplicationMessageRouteContext
    {
        internal MqttApplicationMessageRouteContext(
            IServiceProvider services,
            MqttApplicationMessage message,
            string? clientId,
            IReadOnlyDictionary<string, string> routeValues,
            CancellationToken cancellationToken)
        {
            Services = services;
            Message = message;
            ClientId = clientId;
            RouteValues = routeValues;
            CancellationToken = cancellationToken;
        }

        public IServiceProvider Services { get; }

        public MqttApplicationMessage Message { get; }

        public string? ClientId { get; }

        public IReadOnlyDictionary<string, string> RouteValues { get; }

        public CancellationToken CancellationToken { get; }

        public string GetRouteValue(string key)
        {
            if (RouteValues.TryGetValue(key, out var value))
            {
                return value;
            }

            throw new KeyNotFoundException($"Route value '{key}' was not found.");
        }
    }
}
