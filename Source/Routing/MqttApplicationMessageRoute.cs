// Copyright (c) Atlas Lift Tech Inc. All rights reserved.

using MQTTnet;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;

#nullable enable

namespace MQTTnet.AspNetCore.Routing
{
    internal abstract class MqttApplicationMessageRoute
    {
        private static readonly char[] Separator = new[] { '/' };
        internal static readonly IReadOnlyDictionary<string, string> EmptyRouteValues = EmptyRouteValuesDictionary.Instance;

        private readonly RouteSegment[] _segments;

        private protected MqttApplicationMessageRoute(string template)
        {
            Template = template ?? throw new ArgumentNullException(nameof(template));
            _segments = ParseTemplate(template);
        }

        public string Template { get; }

        public MqttRouteModel RouteModel { get; private protected set; } = null!;

        public static MqttApplicationMessageRoute Create<TPayload>(
            string template,
            JsonTypeInfo<TPayload> jsonTypeInfo,
            Func<MqttApplicationMessageRouteContext, TPayload, ValueTask> handler)
        {
            return new JsonPayloadRoute<TPayload>(template, jsonTypeInfo, handler);
        }

        public static MqttApplicationMessageRoute Create(
            string template,
            Func<MqttApplicationMessageRouteContext, ValueTask> handler)
        {
            return new RawMessageRoute(template, handler);
        }

        public bool TryMatch(string topic, out IReadOnlyDictionary<string, string> routeValues)
        {
            var topicSegments = Split(topic);
            if (topicSegments.Length != _segments.Length)
            {
                routeValues = EmptyRouteValues;
                return false;
            }

            Dictionary<string, string>? values = null;
            for (var i = 0; i < _segments.Length; i++)
            {
                var templateSegment = _segments[i];
                var topicSegment = Uri.UnescapeDataString(topicSegments[i]);
                if (templateSegment.ParameterName != null)
                {
                    values ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    values[templateSegment.ParameterName] = topicSegment;
                    continue;
                }

                if (!string.Equals(templateSegment.Literal, topicSegment, StringComparison.OrdinalIgnoreCase))
                {
                    routeValues = EmptyRouteValues;
                    return false;
                }
            }

            routeValues = values == null ? EmptyRouteValues : values;
            return true;
        }

        public abstract ValueTask InvokeAsync(MqttApplicationMessageRouteContext context);

        private static RouteSegment[] ParseTemplate(string template)
        {
            var segments = Split(template);
            var routeSegments = new RouteSegment[segments.Length];
            for (var i = 0; i < segments.Length; i++)
            {
                var segment = segments[i];
                if (segment.Length > 2 && segment[0] == '{' && segment[^1] == '}')
                {
                    routeSegments[i] = new RouteSegment(null, segment[1..^1]);
                }
                else
                {
                    routeSegments[i] = new RouteSegment(segment, null);
                }
            }

            return routeSegments;
        }

        private static MqttRouteModel CreateRouteModel(
            string template,
            RouteSegment[] segments,
            MethodInfo handlerMethod,
            Type? payloadType,
            string? declaredContentType = null,
            string? declaredPayloadFormatterName = null)
        {
            return new MqttRouteModel(
                template,
                MqttRouteKind.ApplicationMessage,
                CreateSegmentDescriptors(segments),
                CreateRouteParameterModels(segments),
                controllerType: null,
                actionMethod: handlerMethod,
                payloadType: payloadType,
                resultType: typeof(ValueTask),
                declaredContentType: declaredContentType,
                declaredPayloadFormatterName: declaredPayloadFormatterName,
                metadata: handlerMethod.GetCustomAttributes(inherit: false).Cast<object>());
        }

        private static MqttRouteSegmentDescriptor[] CreateSegmentDescriptors(RouteSegment[] segments)
        {
            var descriptors = new MqttRouteSegmentDescriptor[segments.Length];
            for (var i = 0; i < segments.Length; i++)
            {
                descriptors[i] = new MqttRouteSegmentDescriptor(
                    segments[i].ParameterName == null ? segments[i].Literal : null,
                    segments[i].ParameterName,
                    isOptional: false,
                    isCatchAll: false,
                    constraints: null);
            }

            return descriptors;
        }

        private static MqttParameterModel[] CreateRouteParameterModels(RouteSegment[] segments)
        {
            return segments
                .Where(segment => segment.ParameterName != null)
                .Select(segment => new MqttParameterModel(
                    segment.ParameterName!,
                    typeof(string),
                    MqttBindingSource.Route))
                .ToArray();
        }

        private static string[] Split(string value)
        {
            return value.Trim('/').Split(Separator, StringSplitOptions.RemoveEmptyEntries);
        }

        private readonly struct RouteSegment
        {
            public RouteSegment(string? literal, string? parameterName)
            {
                Literal = literal;
                ParameterName = parameterName;
            }

            public string? Literal { get; }

            public string? ParameterName { get; }
        }

        private sealed class JsonPayloadRoute<TPayload> : MqttApplicationMessageRoute
        {
            private readonly JsonTypeInfo<TPayload> _jsonTypeInfo;
            private readonly Func<MqttApplicationMessageRouteContext, TPayload, ValueTask> _handler;

            public JsonPayloadRoute(
                string template,
                JsonTypeInfo<TPayload> jsonTypeInfo,
                Func<MqttApplicationMessageRouteContext, TPayload, ValueTask> handler)
                : base(template)
            {
                _jsonTypeInfo = jsonTypeInfo ?? throw new ArgumentNullException(nameof(jsonTypeInfo));
                _handler = handler;
                RouteModel = CreateRouteModel(
                    template,
                    _segments,
                    handler.Method,
                    typeof(TPayload),
                    "application/json",
                    "json");
            }

            public override ValueTask InvokeAsync(MqttApplicationMessageRouteContext context)
            {
                TPayload? payload;
                try
                {
                    payload = MqttJsonPayloadSerializer.Deserialize(context.Message.Payload, _jsonTypeInfo);
                }
                catch (JsonException ex)
                {
                    context.ModelState.AddModelError(
                        "$payload",
                        MqttBindingErrorCode.PayloadDeserializationFailed,
                        "MQTT payload could not be deserialized.");
                    throw new MqttBindingException(
                        context.ModelState,
                        "MQTT payload could not be deserialized.",
                        ex);
                }

                if (payload == null)
                {
                    context.ModelState.AddModelError(
                        "$payload",
                        MqttBindingErrorCode.PayloadDeserializationFailed,
                        "MQTT payload is required.");
                    throw new MqttBindingException(context.ModelState, "MQTT payload is required.");
                }

                return _handler(context, payload);
            }
        }

        private sealed class RawMessageRoute : MqttApplicationMessageRoute
        {
            private readonly Func<MqttApplicationMessageRouteContext, ValueTask> _handler;

            public RawMessageRoute(string template, Func<MqttApplicationMessageRouteContext, ValueTask> handler)
                : base(template)
            {
                _handler = handler;
                RouteModel = CreateRouteModel(template, _segments, handler.Method, payloadType: null);
            }

            public override ValueTask InvokeAsync(MqttApplicationMessageRouteContext context)
            {
                return _handler(context);
            }
        }

        private sealed class EmptyRouteValuesDictionary : IReadOnlyDictionary<string, string>
        {
            public static readonly EmptyRouteValuesDictionary Instance = new EmptyRouteValuesDictionary();

            public string this[string key] => throw new KeyNotFoundException();

            public IEnumerable<string> Keys => Array.Empty<string>();

            public IEnumerable<string> Values => Array.Empty<string>();

            public int Count => 0;

            public bool ContainsKey(string key)
            {
                return false;
            }

            public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
            {
                return ((IEnumerable<KeyValuePair<string, string>>)Array.Empty<KeyValuePair<string, string>>()).GetEnumerator();
            }

            public bool TryGetValue(string key, out string value)
            {
                value = "";
                return false;
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }
    }
}
