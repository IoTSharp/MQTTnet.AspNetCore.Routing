// Copyright (c) Atlas Lift Tech Inc. All rights reserved.

using Microsoft.Extensions.DependencyInjection;
using System;

#nullable enable

namespace MQTTnet.AspNetCore.Routing
{
    public static class MqttApplicationMessageRoutingServiceCollectionExtensions
    {
        public static IServiceCollection AddMqttApplicationMessageRouting(
            this IServiceCollection services,
            Action<MqttApplicationMessageRouteBuilder> configure)
        {
            return services.AddMqttApplicationMessageSlimRouting(configure);
        }

        public static IServiceCollection AddMqttApplicationMessageSlimRouting(
            this IServiceCollection services,
            Action<MqttApplicationMessageRouteBuilder> configure)
        {
            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            var builder = new MqttApplicationMessageRouteBuilder();
            configure(builder);

            services.AddSingleton(builder.Build());
            services.AddSingleton<IMqttApplicationMessageDispatcher, MqttApplicationMessageDispatcher>();
            return services;
        }
    }
}
