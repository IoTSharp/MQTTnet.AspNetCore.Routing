using System;
using System.Collections.Generic;

#nullable enable

namespace MQTTnet.AspNetCore.Routing
{
    internal static class MqttRouteCatalogDiagnostics
    {
        public const string AmbiguousRouteCode = "MQTTRoute001";

        public static IReadOnlyList<MqttRouteDiagnostic> CreateDiagnostics(IReadOnlyList<MqttRouteModel> routes)
        {
            var diagnostics = new List<MqttRouteDiagnostic>();
            for (var i = 0; i < routes.Count; i++)
            {
                for (var j = i + 1; j < routes.Count; j++)
                {
                    if (!AreAmbiguous(routes[i], routes[j]))
                    {
                        continue;
                    }

                    diagnostics.Add(new MqttRouteDiagnostic(
                        MqttRouteDiagnosticSeverity.Error,
                        AmbiguousRouteCode,
                        $"Route '{routes[i].Template}' ({Describe(routes[i])}) is ambiguous with route '{routes[j].Template}' ({Describe(routes[j])}).",
                        new[] { routes[i], routes[j] }));
                }
            }

            return diagnostics;
        }

        private static bool AreAmbiguous(MqttRouteModel left, MqttRouteModel right)
        {
            if (left.Segments.Count != right.Segments.Count)
            {
                return false;
            }

            for (var i = 0; i < left.Segments.Count; i++)
            {
                var leftSegment = left.Segments[i];
                var rightSegment = right.Segments[i];

                if (leftSegment.IsCatchAll != rightSegment.IsCatchAll)
                {
                    return false;
                }

                if (leftSegment.IsParameter != rightSegment.IsParameter)
                {
                    return false;
                }

                if (!leftSegment.IsParameter)
                {
                    if (!string.Equals(leftSegment.Literal, rightSegment.Literal, StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }

                    continue;
                }

                if (leftSegment.IsOptional != rightSegment.IsOptional)
                {
                    return false;
                }

                if (leftSegment.Constraints.Count != rightSegment.Constraints.Count)
                {
                    return false;
                }
            }

            return true;
        }

        private static string Describe(MqttRouteModel route)
        {
            if (route.ControllerType != null && route.ActionMethod != null)
            {
                return $"{route.ControllerType.FullName}.{route.ActionMethod.Name}";
            }

            if (route.ActionMethod != null)
            {
                return $"{route.ActionMethod.DeclaringType?.FullName ?? "<delegate>"}.{route.ActionMethod.Name}";
            }

            return route.Kind.ToString();
        }
    }
}
