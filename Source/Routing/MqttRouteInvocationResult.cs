using System;
using System.Collections.Generic;

#nullable enable

namespace MQTTnet.AspNetCore.Routing
{
    internal sealed class MqttRouteInvocationResult
    {
        private static readonly IReadOnlyDictionary<string, object> EmptyRouteValues =
            new Dictionary<string, object>(0);

        private MqttRouteInvocationResult(
            bool isMatched,
            MqttRouteModel? route,
            IReadOnlyDictionary<string, object>? routeValues,
            MqttModelStateDictionary modelState)
        {
            IsMatched = isMatched;
            Route = route;
            RouteValues = routeValues ?? EmptyRouteValues;
            ModelState = modelState ?? throw new ArgumentNullException(nameof(modelState));
        }

        public bool IsMatched { get; }

        public MqttRouteModel? Route { get; }

        public IReadOnlyDictionary<string, object> RouteValues { get; }

        public MqttModelStateDictionary ModelState { get; }

        public static MqttRouteInvocationResult Ignored()
        {
            return new MqttRouteInvocationResult(false, null, EmptyRouteValues, new MqttModelStateDictionary());
        }

        public static MqttRouteInvocationResult Unmatched(MqttModelStateDictionary? modelState = null)
        {
            return new MqttRouteInvocationResult(false, null, EmptyRouteValues, modelState ?? new MqttModelStateDictionary());
        }

        public static MqttRouteInvocationResult Matched(
            MqttRouteModel? route,
            IReadOnlyDictionary<string, object>? routeValues,
            MqttModelStateDictionary modelState)
        {
            return new MqttRouteInvocationResult(true, route, routeValues, modelState);
        }
    }
}
