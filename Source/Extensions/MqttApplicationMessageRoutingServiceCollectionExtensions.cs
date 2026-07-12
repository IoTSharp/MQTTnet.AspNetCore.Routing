// Copyright (c) Atlas Lift Tech Inc. All rights reserved.

using Microsoft.Extensions.DependencyInjection;
using MQTTnet.Server;
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

            var routeTable = builder.Build();
            services.AddSingleton(routeTable);
            services.AddSingleton(routeTable.Catalog);
            services.AddSingleton<IMqttApplicationMessageDispatcher, MqttApplicationMessageDispatcher>();
            return services;
        }

        /// <summary>
        /// 将 reflection-free application message routes 挂到 MQTT server PUBLISH 事件。
        /// 未匹配消息保持前序 handler 的处置结果不变，便于与 attribute routing 并行迁移。
        /// </summary>
        public static void WithApplicationMessageRouting(this MqttServer server, IServiceProvider serviceProvider)
        {
            if (server == null)
            {
                throw new ArgumentNullException(nameof(server));
            }

            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            var dispatcher = serviceProvider.GetRequiredService<IMqttApplicationMessageDispatcher>();
            server.InterceptingPublishAsync += async args =>
            {
                await dispatcher.DispatchAsync(args, server, args.CancellationToken).ConfigureAwait(false);
            };
        }
    }
}
