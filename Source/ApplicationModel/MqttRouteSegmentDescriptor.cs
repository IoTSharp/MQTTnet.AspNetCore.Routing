using System;
using System.Collections.Generic;

#nullable enable

namespace MQTTnet.AspNetCore.Routing
{
    internal readonly struct MqttRouteSegmentDescriptor
    {
        public MqttRouteSegmentDescriptor(
            string? literal,
            string? parameterName,
            bool isOptional,
            bool isCatchAll,
            IReadOnlyList<string>? constraints)
        {
            Literal = literal;
            ParameterName = parameterName;
            IsOptional = isOptional;
            IsCatchAll = isCatchAll;
            Constraints = constraints ?? Array.Empty<string>();
        }

        public string? Literal { get; }

        public string? ParameterName { get; }

        public bool IsParameter => ParameterName != null;

        public bool IsOptional { get; }

        public bool IsCatchAll { get; }

        public IReadOnlyList<string> Constraints { get; }
    }
}
