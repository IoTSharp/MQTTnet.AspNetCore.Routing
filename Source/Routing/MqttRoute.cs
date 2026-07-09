// Copyright (c) .NET Foundation. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt
// in the project root for license information.

// Modifications Copyright (c) Atlas Lift Tech Inc. All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using System.Reflection;

namespace MQTTnet.AspNetCore.Routing
{
    [DebuggerDisplay("Handler = {Handler}, Template = {Template}")]
    internal class MqttRoute
    {
        private const DynamicallyAccessedMemberTypes ControllerMemberTypes =
            DynamicallyAccessedMemberTypes.PublicConstructors |
            DynamicallyAccessedMemberTypes.PublicMethods |
            DynamicallyAccessedMemberTypes.PublicProperties |
            DynamicallyAccessedMemberTypes.NonPublicProperties;

        public MqttRoute(
            RouteTemplate template,
            MethodInfo handler,
            string[] unusedRouteParameterNames,
            [DynamicallyAccessedMembers(ControllerMemberTypes)] Type controllerType)
            : this(template, handler, unusedRouteParameterNames, controllerType, null)
        {
        }

        public MqttRoute(
            RouteTemplate template,
            MethodInfo handler,
            string[] unusedRouteParameterNames,
            [DynamicallyAccessedMembers(ControllerMemberTypes)] Type controllerType,
            MqttRouteModel routeModel)
        {
            Template = template;
            UnusedRouteParameterNames = unusedRouteParameterNames;
            Handler = handler;
            ControllerType = controllerType;
            RouteModel = routeModel;
        }

        public RouteTemplate Template { get; }
        public string[] UnusedRouteParameterNames { get; }
        public MethodInfo Handler { get; }
        [DynamicallyAccessedMembers(ControllerMemberTypes)]
        public Type ControllerType { get; }
        public RouteTemplate ControllerTemplate { get; internal set; }
        public bool HaveControllerParameter { get; internal set; }
        public MqttRouteModel RouteModel { get; }
        public PropertyInfo[] ControllerContextProperties { get; internal set; } = Array.Empty<PropertyInfo>();
        public MqttControllerRoutePropertyBinder[] ControllerRoutePropertyBinders { get; internal set; } = Array.Empty<MqttControllerRoutePropertyBinder>();

        internal bool HasLiteralFirstSegment(out string literal)
        {
            if (Template.Segments.Count > 0 && !Template.Segments[0].IsParameter)
            {
                literal = Template.Segments[0].Value;
                return true;
            }

            literal = null;
            return false;
        }

        internal void Match(MqttRouteMatchContext context, StringComparer topicSegmentComparer)
        {
            string catchAllValue = null;
            var templateSegments = Template.Segments;
            var templateSegmentCount = templateSegments.Count;
            var topicSegments = context.Segments;
            var topicSegmentCount = topicSegments.Length;

            // If this template contains a catch-all parameter, we can concatenate the pathSegments at and beyond the
            // catch-all segment's position. For example:
            // Template:        /foo/bar/{*catchAll}
            // PathSegments:    /foo/bar/one/two/three
            if (Template.ContainsCatchAllSegment && topicSegmentCount >= templateSegmentCount)
            {
                catchAllValue = string.Join("/", topicSegments, templateSegmentCount - 1, topicSegmentCount - templateSegmentCount + 1);
            }

            // If there are no optional segments on the route and the length of the route and the template do not match,
            // then there is no chance of this matching and we can bail early.
            else if (Template.OptionalSegmentsCount == 0 && templateSegmentCount != topicSegmentCount)
            {
                return;
            }

            // Parameters will be lazily initialized.
            Dictionary<string, object> parameters = null;
            var numMatchingSegments = 0;

            for (var i = 0; i < templateSegmentCount; i++)
            {
                var segment = templateSegments[i];

                if (segment.IsCatchAll)
                {
                    numMatchingSegments += 1;
                    parameters ??= new Dictionary<string, object>(StringComparer.Ordinal);
                    parameters[segment.Value] = catchAllValue;
                    break;
                }

                // If the template contains more segments than the path, then we may need to break out of this for-loop.
                // This can happen in one of two cases: // (1) If we are comparing a literal route with a literal
                // template and the route is shorter than the template. (2) If we are comparing a template where the
                // last value is an optional parameter that the route does not provide.
                if (i >= topicSegmentCount)
                {
                    // If we are under condition (1) above then we can stop evaluating matches on the rest of this template.
                    if (!segment.IsParameter && !segment.IsOptional)
                    {
                        break;
                    }
                }

                string pathSegment = null;

                if (i < topicSegmentCount)
                {
                    pathSegment = topicSegments[i];
                }

                if (!segment.Match(pathSegment, topicSegmentComparer, out var matchedParameterValue))
                {
                    break;
                }
                else
                {
                    numMatchingSegments++;

                    if (segment.IsParameter)
                    {
                        parameters ??= new Dictionary<string, object>(StringComparer.Ordinal);
                        parameters[segment.Value] = matchedParameterValue;
                    }
                }
            }

            // In addition to extracting parameter values from the URL, each route entry also knows which other
            // parameters should be supplied with null values. These are parameters supplied by other route entries
            // matching the same handler.
            if (!Template.ContainsCatchAllSegment && UnusedRouteParameterNames.Length > 0)
            {
                parameters ??= new Dictionary<string, object>(StringComparer.Ordinal);

                for (var i = 0; i < UnusedRouteParameterNames.Length; i++)
                {
                    parameters[UnusedRouteParameterNames[i]] = null;
                }
            }

            // We track the number of segments in the template that matched against this particular route then only
            // select the route that matches the most number of segments on the route that was passed. This check is an
            // exactness check that favors the more precise of two templates in the event that the following route table
            // exists. Route 1: /{anythingGoes} Route 2: /users/{id:int} And the provided route is `/users/1`. We want
            // to choose Route 2 over Route 1. Furthermore, literal routes are preferred over parameterized routes. If
            // the two routes below are registered in the route table. Route 1: /users/1 Route 2: /users/{id:int} And
            // the provided route is `/users/1`. We want to choose Route 1 over Route 2.
            var allRouteSegmentsMatch = numMatchingSegments >= topicSegmentCount;

            // Checking that all route segments have been matches does not suffice if we are comparing literal templates
            // with literal routes. For example, the template `/this/is/a/template` and the route `/this/`. In that
            // case, we want to ensure that all non-optional segments have matched as well.
            var allNonOptionalSegmentsMatch = numMatchingSegments >= (templateSegmentCount - Template.OptionalSegmentsCount);

            if (Template.ContainsCatchAllSegment || (allRouteSegmentsMatch && allNonOptionalSegmentsMatch))
            {
                context.Parameters = parameters;
                context.Handler = Handler;
                context.ControllerType = ControllerType;
                context.HaveControllerParameter = HaveControllerParameter;
                context.ControllerTemplate = ControllerTemplate;
                context.RouteModel = RouteModel;
                context.ControllerContextProperties = ControllerContextProperties;
                context.ControllerRoutePropertyBinders = ControllerRoutePropertyBinders;
            }
        }
    }

    internal sealed class MqttControllerRoutePropertyBinder
    {
        private readonly PropertyInfo _property;

        public MqttControllerRoutePropertyBinder(string routeValueName, PropertyInfo property)
        {
            RouteValueName = routeValueName;
            _property = property;
        }

        public string RouteValueName { get; }

        public Type PropertyType => _property.PropertyType;

        public void SetValue(object controller, object value)
        {
            _property.SetValue(controller, value);
        }
    }
}
