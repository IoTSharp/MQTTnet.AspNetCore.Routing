// Copyright (c) .NET Foundation. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt
// in the project root for license information.

// Modifications Copyright (c) Atlas Lift Tech Inc. All rights reserved.

using Microsoft.AspNetCore.Mvc;
using MQTTnet;
using MQTTnet.AspNetCore.Routing.Attributes;
using MQTTnet.Server;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace MQTTnet.AspNetCore.Routing
{
    internal static class MqttRouteTableFactory
    {
        private const DynamicallyAccessedMemberTypes ControllerMemberTypes =
            DynamicallyAccessedMemberTypes.PublicConstructors |
            DynamicallyAccessedMemberTypes.PublicMethods |
            DynamicallyAccessedMemberTypes.PublicProperties |
            DynamicallyAccessedMemberTypes.NonPublicProperties;

        private static readonly ConcurrentDictionary<Key, MqttRouteTable> Cache = new ConcurrentDictionary<Key, MqttRouteTable>();
        public static readonly IComparer<MqttRoute> RoutePrecedence = Comparer<MqttRoute>.Create(RouteComparison);

        /// <summary>
        /// Given a list of assemblies, find all instances of MqttControllers and wire up routing for them. Instances of
        /// controllers must inherit from MqttBaseController and be decorated with an MqttRoute attribute.
        /// </summary>
        /// <param name="assembly">Assemblies to scan for routes</param>
        /// <returns></returns>
        [RequiresUnreferencedCode("Assembly scanning cannot be statically analyzed. Use CreateFromControllerTypes for Native AOT applications.")]
        internal static MqttRouteTable Create(IEnumerable<Assembly> assemblies)
        {
            var asm = (assemblies ?? new Assembly[] { Assembly.GetExecutingAssembly() }).ToArray();
            var key = new Key(asm.OrderBy(a => a.FullName).ToArray());

            if (Cache.TryGetValue(key, out var resolvedComponents))
            {
                return resolvedComponents;
            }

            var actions = asm.SelectMany(a => a.GetTypes())
                .SelectMany(GetControllerActions);

            var routeTable = Create(CreateApplicationModel(actions));

            Cache.TryAdd(key, routeTable);

            return routeTable;
        }

        [RequiresUnreferencedCode("Controller type arrays cannot be statically analyzed. Use CreateFromControllerType<TController> for Native AOT applications.")]
        internal static MqttRouteTable CreateFromControllerTypes(IEnumerable<Type> controllerTypes)
        {
            var actions = controllerTypes
                .SelectMany(GetControllerActions);

            return Create(CreateApplicationModel(actions));
        }

        internal static MqttRouteTable CreateFromControllerType<[DynamicallyAccessedMembers(ControllerMemberTypes)] TController>()
        {
            return CreateFromControllerType(typeof(TController));
        }

        internal static MqttRouteTable CreateFromControllerType(
            [DynamicallyAccessedMembers(ControllerMemberTypes)] Type controllerType)
        {
            var actions = Array.Empty<MethodInfo>();
            if (controllerType.GetCustomAttribute(typeof(MqttControllerAttribute), true) != null)
            {
                actions = GetControllerActions(controllerType)
                    .Select(action => action.Method)
                    .ToArray();
            }

            return Create(CreateApplicationModel(actions.Select(action => new MqttControllerAction(action, controllerType))));
        }

        internal static MqttRouteTable Create(IEnumerable<MqttControllerAction> actions)
        {
            return Create(CreateApplicationModel(actions));
        }

        internal static MqttApplicationModel CreateApplicationModel(IEnumerable<MqttControllerAction> actions)
        {
            var templatesByHandler = new Dictionary<MqttControllerAction, MqttControllerRouteTemplate[]>();

            foreach (var action in actions)
            {
                var controllerTemplates = action.ControllerType.GetCustomAttributes<MqttRouteAttribute>(inherit: false)
                    .Select(c => ReplaceTokens(c.Template, action.ControllerType.Name, action.Method.Name))
                    .ToArray();

                var routeAttributes = action.Method.GetCustomAttributes<MqttRouteAttribute>(inherit: false)
                    .Select(a => ReplaceTokens(a.Template, action.ControllerType.Name, action.Method.Name))
                    .ToArray();

                if (controllerTemplates.Length == 0)
                {
                    controllerTemplates = new string[] { "" };
                }

                // If an action doesn't have a route attribute on it, we use the action name. Unlike Mvc/WebAPI we don't
                // need to strip the "Get", "Put", etc. prefixes from the action because MQTT doesn't have verbs by convention.
                if (routeAttributes.Length == 0)
                {
                    routeAttributes = new string[] { action.Method.Name };
                }

                // If an action starts with a /, we throw away the inherited portion of the path. We don't process ~/
                // because it wouldn't make sense in the context of Mqtt routing which has no concept of relative paths.
                var templates = controllerTemplates.SelectMany(
                    _ => routeAttributes,
                    (controllerTemplate, routeTemplate) => new MqttControllerRouteTemplate(
                        CombineTemplates(controllerTemplate, routeTemplate),
                        controllerTemplate)).ToArray();
              
                templatesByHandler.Add(action, templates);
            }

            return CreateApplicationModel(templatesByHandler);
        }

        /// <summary>
        /// Generate routes given a collection of MethodInfo objects and templates that should call those methods
        /// </summary>
        /// <param name="templatesByHandler">Templates that should route to each handler</param>
        internal static MqttRouteTable Create(Dictionary<MqttControllerAction, MqttControllerRouteTemplate[]> templatesByHandler)
        {
            return Create(CreateApplicationModel(templatesByHandler));
        }

        internal static MqttApplicationModel CreateApplicationModel(Dictionary<MqttControllerAction, MqttControllerRouteTemplate[]> templatesByHandler)
        {
            var routes = new List<MqttRouteModel>();
            var actionsByController = new Dictionary<Type, List<MqttActionModel>>();
            var controllerRoutesByController = new Dictionary<Type, List<MqttRouteModel>>();
            var controllerRouteKeys = new HashSet<string>(StringComparer.Ordinal);

            foreach (var keyValuePair in templatesByHandler)
            {
                var methodInfo = keyValuePair.Key.Method;
                var controllerType = keyValuePair.Key.ControllerType;
                var actionParameters = methodInfo.GetParameters()
                    .Select(CreateActionParameterModel)
                    .ToArray();
                var payloadParameter = MqttActionModel.FindPayloadParameter(actionParameters);
                var payloadType = payloadParameter?.ParameterType;
                var declaredContentType = payloadParameter?.DeclaredContentType;
                var declaredFormatterName = payloadParameter?.FormatterName;
                var parsedTemplates = keyValuePair.Value
                    .Select(v => new ParsedMqttControllerRouteTemplate(v, TemplateParser.ParseTemplate(v.RouteTemplate)))
                    .ToArray();

                var allRouteParameterNames = parsedTemplates
                    .SelectMany(v => GetParameterNames(v.ParsedRouteTemplate))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                var actionRoutes = new List<MqttRouteModel>();
                foreach (var parsedTemplate in parsedTemplates)
                {
                    var unusedRouteParameterNames = allRouteParameterNames
                        .Except(GetParameterNames(parsedTemplate.ParsedRouteTemplate), StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                    RouteTemplate parsedControllerTemplate = null;
                    if (!string.IsNullOrEmpty(parsedTemplate.RouteTemplate.ControllerTemplate))
                    {
                        parsedControllerTemplate = TemplateParser.ParseTemplate(parsedTemplate.RouteTemplate.ControllerTemplate);
                        AddControllerRouteModel(
                            controllerRoutesByController,
                            controllerRouteKeys,
                            controllerType,
                            parsedTemplate.RouteTemplate.ControllerTemplate,
                            parsedControllerTemplate);
                    }

                    var routeModel = new MqttRouteModel(
                        parsedTemplate.RouteTemplate.RouteTemplate,
                        MqttRouteKind.ControllerAction,
                        CreateSegmentDescriptors(parsedTemplate.ParsedRouteTemplate),
                        CreateRouteParameterModels(parsedTemplate.ParsedRouteTemplate),
                        controllerType,
                        methodInfo,
                        parsedTemplate.RouteTemplate.ControllerTemplate,
                        payloadType,
                        methodInfo.ReturnType,
                        declaredContentType,
                        declaredFormatterName,
                        metadata: methodInfo.GetCustomAttributes(inherit: true).Cast<object>(),
                        parsedTemplate: parsedTemplate.ParsedRouteTemplate,
                        parsedControllerTemplate: parsedControllerTemplate,
                        unusedRouteParameterNames: unusedRouteParameterNames);

                    routes.Add(routeModel);
                    actionRoutes.Add(routeModel);
                }

                var actionModel = new MqttActionModel(
                    controllerType,
                    methodInfo,
                    actionRoutes,
                    actionParameters,
                    payloadType,
                    methodInfo.ReturnType,
                    declaredContentType,
                    declaredFormatterName,
                    metadata: methodInfo.GetCustomAttributes(inherit: true).Cast<object>());

                if (!actionsByController.TryGetValue(controllerType, out var controllerActions))
                {
                    controllerActions = new List<MqttActionModel>();
                    actionsByController.Add(controllerType, controllerActions);
                }

                controllerActions.Add(actionModel);
            }

            var controllers = actionsByController
                .OrderBy(pair => pair.Key.FullName, StringComparer.Ordinal)
                .Select(pair => new MqttControllerModel(
                    pair.Key,
                    controllerRoutesByController.TryGetValue(pair.Key, out var controllerRoutes)
                        ? controllerRoutes
                        : Array.Empty<MqttRouteModel>(),
                    pair.Value,
                    metadata: pair.Key.GetCustomAttributes(inherit: true).Cast<object>()))
                .ToArray();

            return new MqttApplicationModel(controllers, routes);
        }

        internal static MqttRouteTable Create(MqttApplicationModel applicationModel)
        {
            var catalogBeforeSorting = new MqttRouteCatalog(applicationModel);
            catalogBeforeSorting.ThrowIfErrors();

            var routes = new List<MqttRoute>();

            foreach (var routeModel in applicationModel.Routes)
            {
                if (routeModel.Kind != MqttRouteKind.ControllerAction)
                {
                    continue;
                }

                if (routeModel.ParsedTemplate == null || routeModel.ActionMethod == null || routeModel.ControllerType == null)
                {
                    throw new InvalidOperationException($"Controller route '{routeModel.Template}' is missing required route metadata.");
                }

                var entry = new MqttRoute(
                    routeModel.ParsedTemplate,
                    routeModel.ActionMethod,
                    routeModel.UnusedRouteParameterNames,
                    routeModel.ControllerType,
                    routeModel);
                if (routeModel.ParsedControllerTemplate != null)
                {
                    entry.ControllerTemplate = routeModel.ParsedControllerTemplate;
                    entry.HaveControllerParameter = entry.ControllerTemplate.Segments.Any(c => c.IsParameter);
                }

                routes.Add(entry);
            }

            var sortedRoutes = routes.OrderBy(id => id, RoutePrecedence).ToArray();
            var catalog = new MqttRouteCatalog(
                applicationModel,
                sortedRoutes
                    .Where(route => route.RouteModel != null)
                    .Select(route => route.RouteModel),
                catalogBeforeSorting.Diagnostics);
            return new MqttRouteTable(sortedRoutes, catalog);
        }

        private static void AddControllerRouteModel(
            Dictionary<Type, List<MqttRouteModel>> controllerRoutesByController,
            HashSet<string> controllerRouteKeys,
            [DynamicallyAccessedMembers(ControllerMemberTypes)]
            Type controllerType,
            string controllerTemplate,
            RouteTemplate parsedControllerTemplate)
        {
            var key = $"{controllerType.AssemblyQualifiedName}|{controllerTemplate}";
            if (!controllerRouteKeys.Add(key))
            {
                return;
            }

            if (!controllerRoutesByController.TryGetValue(controllerType, out var controllerRoutes))
            {
                controllerRoutes = new List<MqttRouteModel>();
                controllerRoutesByController.Add(controllerType, controllerRoutes);
            }

            controllerRoutes.Add(new MqttRouteModel(
                controllerTemplate,
                MqttRouteKind.ControllerAction,
                CreateSegmentDescriptors(parsedControllerTemplate),
                CreateRouteParameterModels(parsedControllerTemplate),
                controllerType,
                parsedTemplate: parsedControllerTemplate));
        }

        private static MqttParameterModel CreateActionParameterModel(ParameterInfo parameter)
        {
            var bindingSource = GetActionParameterBindingSource(parameter);
            var payloadAttribute = parameter.GetCustomAttribute<FromMqttPayloadAttribute>(inherit: false);

            TryGetParameterDefaultValue(parameter, out var defaultValue);
            return new MqttParameterModel(
                parameter.Name ?? "$parameter",
                parameter.ParameterType,
                bindingSource,
                parameter,
                parameter.IsOptional || parameter.HasDefaultValue,
                defaultValue,
                metadata: parameter.GetCustomAttributes(inherit: false).Cast<object>(),
                bindingName: GetActionParameterBindingName(parameter, bindingSource),
                declaredContentType: payloadAttribute?.ContentType,
                formatterName: payloadAttribute?.FormatterName);
        }

        private static string GetActionParameterBindingName(ParameterInfo parameter, MqttBindingSource bindingSource)
        {
            switch (bindingSource)
            {
                case MqttBindingSource.Route:
                    return parameter.GetCustomAttribute<FromMqttRouteAttribute>(inherit: false)?.Name
                        ?? parameter.Name
                        ?? "$parameter";
                case MqttBindingSource.Session:
                    return parameter.GetCustomAttribute<FromMqttSessionAttribute>(inherit: false)?.Key
                        ?? parameter.Name
                        ?? "$parameter";
                case MqttBindingSource.Client:
                    return parameter.GetCustomAttribute<FromMqttClientAttribute>(inherit: false)?.Name
                        ?? "clientId";
                case MqttBindingSource.UserProperty:
                    return parameter.GetCustomAttribute<FromMqttUserPropertyAttribute>(inherit: false)?.Name
                        ?? parameter.Name
                        ?? "$parameter";
                case MqttBindingSource.Payload:
                    return "$payload";
                case MqttBindingSource.Context:
                case MqttBindingSource.Services:
                case MqttBindingSource.Unknown:
                default:
                    return parameter.Name ?? "$parameter";
            }
        }

        private static MqttBindingSource GetActionParameterBindingSource(ParameterInfo parameter)
        {
            if (parameter.IsDefined(typeof(FromMqttRouteAttribute), inherit: false))
            {
                return MqttBindingSource.Route;
            }

            if (parameter.IsDefined(typeof(FromMqttPayloadAttribute), inherit: false)
                || parameter.IsDefined(typeof(FromPayloadAttribute), inherit: false))
            {
                return MqttBindingSource.Payload;
            }

            if (parameter.IsDefined(typeof(FromMqttSessionAttribute), inherit: false))
            {
                return MqttBindingSource.Session;
            }

            if (parameter.IsDefined(typeof(FromMqttClientAttribute), inherit: false))
            {
                return MqttBindingSource.Client;
            }

            if (parameter.IsDefined(typeof(FromMqttUserPropertyAttribute), inherit: false))
            {
                return MqttBindingSource.UserProperty;
            }

            if (parameter.IsDefined(typeof(FromMqttContextAttribute), inherit: false)
                || IsContextParameterType(parameter.ParameterType))
            {
                return MqttBindingSource.Context;
            }

            return MqttBindingSource.Route;
        }

        private static bool IsContextParameterType(Type parameterType)
        {
            return parameterType == typeof(MqttActionContext)
                || parameterType == typeof(MqttRequestContext)
                || parameterType == typeof(MqttRouteContext)
                || parameterType == typeof(MqttModelStateDictionary)
                || parameterType == typeof(MqttApplicationMessage)
                || parameterType == typeof(IServiceProvider)
                || parameterType == typeof(CancellationToken)
                || parameterType == typeof(MqttServer);
        }

        private static MqttParameterModel[] CreateRouteParameterModels(RouteTemplate routeTemplate)
        {
            return routeTemplate.Segments
                .Where(segment => segment.IsParameter)
                .Select(segment => new MqttParameterModel(
                    segment.Value,
                    typeof(string),
                    MqttBindingSource.Route,
                    isOptional: segment.IsOptional,
                    routeConstraints: GetRouteConstraintNames(routeTemplate, segment)))
                .ToArray();
        }

        private static MqttRouteSegmentDescriptor[] CreateSegmentDescriptors(RouteTemplate routeTemplate)
        {
            return routeTemplate.Segments
                .Select(segment => new MqttRouteSegmentDescriptor(
                    segment.IsParameter ? null : segment.Value,
                    segment.IsParameter ? segment.Value : null,
                    segment.IsOptional,
                    segment.IsCatchAll,
                    GetRouteConstraintNames(routeTemplate, segment)))
                .ToArray();
        }

        private static string[] GetRouteConstraintNames(RouteTemplate routeTemplate, TemplateSegment segment)
        {
            if (!segment.IsParameter || segment.Constraints.Length == 0)
            {
                return Array.Empty<string>();
            }

            var rawSegments = routeTemplate.TemplateText.Split('/');
            for (var i = 0; i < rawSegments.Length; i++)
            {
                var rawSegment = rawSegments[i];
                if (rawSegment.Length < 3 || rawSegment[0] != '{' || rawSegment[rawSegment.Length - 1] != '}')
                {
                    continue;
                }

                var parameterText = rawSegment.Substring(1, rawSegment.Length - 2);
                if (parameterText.StartsWith("*", StringComparison.Ordinal))
                {
                    parameterText = parameterText.Substring(1);
                }

                var tokens = parameterText.Split(':');
                var parameterName = tokens[0];
                if (parameterName.EndsWith("?", StringComparison.Ordinal))
                {
                    parameterName = parameterName.Substring(0, parameterName.Length - 1);
                }

                if (string.Equals(parameterName, segment.Value, StringComparison.OrdinalIgnoreCase))
                {
                    return tokens.Skip(1).ToArray();
                }
            }

            return Array.Empty<string>();
        }

        private static bool TryGetParameterDefaultValue(ParameterInfo parameter, out object defaultValue)
        {
            if (parameter.HasDefaultValue)
            {
                defaultValue = parameter.DefaultValue == DBNull.Value ? null : parameter.DefaultValue;
                return true;
            }

            if (parameter.IsOptional)
            {
                defaultValue = null;
                return true;
            }

            defaultValue = null;
            return false;
        }

        private static IEnumerable<MqttControllerAction> GetControllerActions(
            [DynamicallyAccessedMembers(ControllerMemberTypes)] Type controllerType)
        {
            if (controllerType.GetCustomAttribute(typeof(MqttControllerAttribute), true) == null)
            {
                yield break;
            }

            var seenBaseDefinitions = new HashSet<MethodInfo>();
            var currentType = controllerType;
            while (currentType != null
                && currentType != typeof(object)
                && currentType != typeof(MqttBaseController))
            {
                var methods = currentType
                    .GetMethods(BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.Public)
                    .Where(IsActionMethod);

                foreach (var method in methods)
                {
                    if (seenBaseDefinitions.Add(method.GetBaseDefinition()))
                    {
                        yield return new MqttControllerAction(method, controllerType);
                    }
                }

                currentType = currentType.BaseType;
            }
        }

        private static bool IsActionMethod(MethodInfo method)
        {
            return !method.GetCustomAttributes(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), true).Any()
                && !method.IsDefined(typeof(NonActionAttribute));
        }

        private static string CombineTemplates(string controllerTemplate, string routeTemplate)
        {
            if (string.IsNullOrEmpty(controllerTemplate))
            {
                return routeTemplate;
            }

            if (string.IsNullOrEmpty(routeTemplate) || string.Equals(routeTemplate, "/", StringComparison.Ordinal))
            {
                return controllerTemplate;
            }

            if (routeTemplate[0] == '/')
            {
                return routeTemplate;
            }

            return $"{controllerTemplate.TrimEnd('/')}/{routeTemplate.TrimStart('/')}";
        }

        /// <summary>
        /// Returns the names of all parameters in a given RouteTemplate
        /// </summary>
        private static string[] GetParameterNames(RouteTemplate routeTemplate)
        {
            return routeTemplate.Segments
                .Where(s => s.IsParameter)
                .Select(s => s.Value)
                .ToArray();
        }

        /// <summary>
        /// Given a route template string suchs a "[controller]/[action]" replace the tokens with the values provided.
        /// /// Controllers with a suffix of "Controller" will be chopped to exclude the word Controller from the
        /// returns route string.
        /// </summary>
        /// <param name="template">Template string</param>
        /// <param name="controllerName">Name of the controller object</param>
        /// <param name="actionName">Name of the action method</param>
        /// <returns>String with replaced values</returns>
        private static string ReplaceTokens(string template, string controllerName, string actionName)
        {
            // In a future enhancement, we may allow escaping tokens with a "[[" to have feature parity with AspNet routing.
            return template
                // Strip "Controller" suffix from controller name if needed
                .Replace("[controller]", controllerName.EndsWith("Controller") ? controllerName.Substring(0, controllerName.Length - 10) : controllerName)
                .Replace("[action]", actionName);
        }

        /// <summary>
        /// Route precedence algorithm. We collect all the routes and sort them from most specific to less specific. The
        /// specificity of a route is given by the specificity of its segments and the position of those segments in the route.
        /// * A literal segment is more specific than a parameter segment.
        /// * A parameter segment with more constraints is more specific than one with fewer constraints
        /// * Segment earlier in the route are evaluated before segments later in the route. For example: /Literal is
        /// more specific than /Parameter /Route/With/{parameter} is more specific than /{multiple}/With/{parameters}
        /// /Product/{id:int} is more specific than /Product/{id}
        ///
        /// Routes can be ambiguous if: They are composed of literals and those literals have the same values (case
        /// insensitive) They are composed of a mix of literals and parameters, in the same relative order and the
        /// literals have the same values. For example:
        /// * /literal and /Literal /{parameter}/literal and /{something}/literal /{parameter:constraint}/literal and /{something:constraint}/literal
        ///
        /// To calculate the precedence we sort the list of routes as follows:
        /// * Shorter routes go first.
        /// * A literal wins over a parameter in precedence.
        /// * For literals with different values (case insensitive) we choose the lexical order
        /// * For parameters with different numbers of constraints, the one with more wins If we get to the end of the
        /// comparison routing we've detected an ambiguous pair of routes.
        /// * Catch-all routes go last
        /// </summary>
        internal static int RouteComparison(MqttRoute x, MqttRoute y)
        {
            if (ReferenceEquals(x, y))
            {
                return 0;
            }

            var xTemplate = x.Template;
            var yTemplate = y.Template;

            if (xTemplate.Segments.Count != y.Template.Segments.Count)
            {
                if (xTemplate.Segments.Count  ==0 ||  y.Template.Segments.Count==0)
                {
                    return -1;
                }
                if (xTemplate.Segments.Count == 0 &&  y.Template.Segments.Count == 0)
                {
                    return 1;
                }
                if (!xTemplate.Segments[xTemplate.Segments.Count - 1].IsCatchAll && yTemplate.Segments[yTemplate.Segments.Count - 1].IsCatchAll)
                {
                    return -1;
                }

                if (xTemplate.Segments[xTemplate.Segments.Count - 1].IsCatchAll && !yTemplate.Segments[yTemplate.Segments.Count - 1].IsCatchAll)
                {
                    return 1;
                }

                return xTemplate.Segments.Count < y.Template.Segments.Count ? -1 : 1;
            }
            else
            {
                for (var i = 0; i < xTemplate.Segments.Count; i++)
                {
                    var xSegment = xTemplate.Segments[i];
                    var ySegment = yTemplate.Segments[i];

                    if (!xSegment.IsCatchAll && ySegment.IsCatchAll)
                    {
                        return -1;
                    }

                    if (xSegment.IsCatchAll && !ySegment.IsCatchAll)
                    {
                        return 1;
                    }

                    if (!xSegment.IsParameter && ySegment.IsParameter)
                    {
                        return -1;
                    }

                    if (xSegment.IsParameter && !ySegment.IsParameter)
                    {
                        return 1;
                    }

                    if (xSegment.IsParameter)
                    {
                        // Always favor non-optional parameters over optional ones
                        if (!xSegment.IsOptional && ySegment.IsOptional)
                        {
                            return -1;
                        }

                        if (xSegment.IsOptional && !ySegment.IsOptional)
                        {
                            return 1;
                        }

                        if (xSegment.Constraints.Length > ySegment.Constraints.Length)
                        {
                            return -1;
                        }
                        else if (xSegment.Constraints.Length < ySegment.Constraints.Length)
                        {
                            return 1;
                        }
                    }
                    else
                    {
                        var comparison = string.Compare(xSegment.Value, ySegment.Value, StringComparison.OrdinalIgnoreCase);

                        if (comparison != 0)
                        {
                            return comparison;
                        }
                    }
                }

                throw new InvalidOperationException($@"The following routes are ambiguous:
'{x.Template.TemplateText}' in '{x.Handler.DeclaringType.FullName}.{x.Handler.Name}'
'{y.Template.TemplateText}' in '{y.Handler.DeclaringType.FullName}.{y.Handler.Name}'
");
            }
        }

        private readonly struct Key : IEquatable<Key>
        {
            public readonly Assembly[] Assemblies;

            public Key(Assembly[] assemblies)
            {
                Assemblies = assemblies;
            }

            public override bool Equals(object obj)
            {
                return obj is Key other ? base.Equals(other) : false;
            }

            public bool Equals(Key other)
            {
                if (Assemblies == null && other.Assemblies == null)
                {
                    return true;
                }
                else if ((Assemblies == null) || (other.Assemblies == null))
                {
                    return false;
                }
                else if (Assemblies.Length != other.Assemblies.Length)
                {
                    return false;
                }

                for (var i = 0; i < Assemblies.Length; i++)
                {
                    if (!Assemblies[i].Equals(other.Assemblies[i]))
                    {
                        return false;
                    }
                }

                return true;
            }

            public override int GetHashCode()
            {
                var hash = new HashCode();

                if (Assemblies != null)
                {
                    for (var i = 0; i < Assemblies.Length; i++)
                    {
                        hash.Add(Assemblies[i]);
                    }
                }

                return hash.ToHashCode();
            }
        }

        internal readonly struct MqttControllerAction
        {
            public MqttControllerAction(
                MethodInfo method,
                [DynamicallyAccessedMembers(ControllerMemberTypes)] Type controllerType)
            {
                Method = method;
                ControllerType = controllerType;
            }

            public MethodInfo Method { get; }

            [DynamicallyAccessedMembers(ControllerMemberTypes)]
            public Type ControllerType { get; }
        }

        internal readonly struct MqttControllerRouteTemplate
        {
            public MqttControllerRouteTemplate(string routeTemplate, string controllerTemplate)
            {
                RouteTemplate = routeTemplate;
                ControllerTemplate = controllerTemplate;
            }

            public string RouteTemplate { get; }

            public string ControllerTemplate { get; }
        }

        private readonly struct ParsedMqttControllerRouteTemplate
        {
            public ParsedMqttControllerRouteTemplate(MqttControllerRouteTemplate routeTemplate, RouteTemplate parsedRouteTemplate)
            {
                RouteTemplate = routeTemplate;
                ParsedRouteTemplate = parsedRouteTemplate;
            }

            public MqttControllerRouteTemplate RouteTemplate { get; }

            public RouteTemplate ParsedRouteTemplate { get; }
        }
    }
}
