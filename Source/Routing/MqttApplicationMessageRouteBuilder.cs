// Copyright (c) Atlas Lift Tech Inc. All rights reserved.

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;

#nullable enable

namespace MQTTnet.AspNetCore.Routing
{
    public sealed class MqttApplicationMessageRouteBuilder
    {
        private readonly List<MqttApplicationMessageRoute> _routes = new List<MqttApplicationMessageRoute>();

        public MqttApplicationMessageRouteBuilder MapJson<TPayload>(
            string template,
            JsonTypeInfo<TPayload> jsonTypeInfo,
            Func<MqttApplicationMessageRouteContext, TPayload, ValueTask> handler)
        {
            if (template == null)
            {
                throw new ArgumentNullException(nameof(template));
            }

            if (jsonTypeInfo == null)
            {
                throw new ArgumentNullException(nameof(jsonTypeInfo));
            }

            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            _routes.Add(MqttApplicationMessageRoute.Create(template, jsonTypeInfo, handler));
            return this;
        }

        public MqttApplicationMessageRouteBuilder Map(
            string template,
            Func<MqttApplicationMessageRouteContext, ValueTask> handler)
        {
            if (template == null)
            {
                throw new ArgumentNullException(nameof(template));
            }

            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            _routes.Add(MqttApplicationMessageRoute.Create(template, handler));
            return this;
        }

        internal MqttApplicationMessageRouteTable Build()
        {
            return new MqttApplicationMessageRouteTable(_routes.ToArray());
        }
    }
}
