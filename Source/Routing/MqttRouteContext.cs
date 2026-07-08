// Copyright (c) .NET Foundation. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt
// in the project root for license information.

// Modifications Copyright (c) Atlas Lift Tech Inc. All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace MQTTnet.AspNetCore.Routing
{
    internal class MqttRouteMatchContext
    {
        private const DynamicallyAccessedMemberTypes ControllerMemberTypes =
            DynamicallyAccessedMemberTypes.PublicConstructors |
            DynamicallyAccessedMemberTypes.PublicMethods |
            DynamicallyAccessedMemberTypes.PublicProperties |
            DynamicallyAccessedMemberTypes.NonPublicProperties;

        private static readonly char[] Separator = new[] { '/' };

        public MqttRouteMatchContext(string path)
        {
            // This is a simplification. We are assuming there are no paths like /a//b/. A proper routing implementation
            // would be more sophisticated.
            Segments = path.Trim('/').Split(Separator, StringSplitOptions.RemoveEmptyEntries);

            // Individual segments are URL-decoded in order to support arbitrary characters, assuming UTF-8 encoding.
            for (int i = 0; i < Segments.Length; i++)
            {
                Segments[i] = Uri.UnescapeDataString(Segments[i]);
            }
        }

        public string[] Segments { get; }

        public MethodInfo Handler { get; set; }

        [DynamicallyAccessedMembers(ControllerMemberTypes)]
        public Type ControllerType { get; set; }
        public IReadOnlyDictionary<string, object> Parameters { get; set; }
        public bool HaveControllerParameter { get; internal set; }
        public RouteTemplate ControllerTemplate { get; internal set; }
        public MqttRouteModel RouteModel { get; internal set; }
    }
}
