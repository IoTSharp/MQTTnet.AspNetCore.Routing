// Copyright (c) Atlas Lift Tech Inc. All rights reserved.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MQTTnet.Server;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace MQTTnet.AspNetCore.Routing
{
    internal sealed class MqttApplicationMessageDispatcher : IMqttApplicationMessageDispatcher
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly MqttApplicationMessageRouteTable _routeTable;
        private readonly ILogger<MqttApplicationMessageDispatcher>? _logger;
        private readonly MqttRoutingOptions? _routingOptions;

        public MqttApplicationMessageDispatcher(
            IServiceScopeFactory scopeFactory,
            MqttApplicationMessageRouteTable routeTable,
            ILogger<MqttApplicationMessageDispatcher>? logger = null,
            MqttRoutingOptions? routingOptions = null)
        {
            _scopeFactory = scopeFactory;
            _routeTable = routeTable;
            _logger = logger;
            _routingOptions = routingOptions;
        }

        public async Task<MqttApplicationMessageDispatchResult> DispatchAsync(
            MqttApplicationMessage applicationMessage,
            string? clientId = null,
            CancellationToken cancellationToken = default)
        {
            if (!_routeTable.TryMatch(applicationMessage.Topic, out var route, out var routeValues) || route == null)
            {
                return new MqttApplicationMessageDispatchResult(false);
            }

            using var scope = _scopeFactory.CreateScope();
            var context = new MqttApplicationMessageRouteContext(
                scope.ServiceProvider,
                applicationMessage,
                clientId,
                routeValues,
                route.RouteModel,
                cancellationToken);

            try
            {
                await route.InvokeAsync(context).ConfigureAwait(false);
                return new MqttApplicationMessageDispatchResult(true, context.ModelState);
            }
            catch (MqttBindingException ex)
            {
                _logger?.LogDebug(
                    ex,
                    "MQTT message binding failed for topic '{Topic}' with {ErrorCount} model state error(s).",
                    applicationMessage.Topic,
                    ex.ModelState.ErrorCount);

                return new MqttApplicationMessageDispatchResult(false, ex.ModelState);
            }
        }

        public async Task<MqttApplicationMessageDispatchResult> DispatchAsync(
            MqttApplicationMessageReceivedEventArgs eventArgs,
            CancellationToken cancellationToken = default)
        {
            var result = await DispatchAsync(eventArgs.ApplicationMessage, eventArgs.ClientId, cancellationToken)
                .ConfigureAwait(false);

            eventArgs.IsHandled = result.IsHandled;
            eventArgs.ProcessingFailed = !result.IsHandled;
            return result;
        }

        public async Task<MqttApplicationMessageDispatchResult> DispatchAsync(
            InterceptingPublishEventArgs eventArgs,
            MqttServer mqttServer,
            CancellationToken cancellationToken = default)
        {
            if (!_routeTable.TryMatch(eventArgs.ApplicationMessage.Topic, out var route, out var routeValues) || route == null)
            {
                return new MqttApplicationMessageDispatchResult(false);
            }

            using var scope = _scopeFactory.CreateScope();
            var context = new MqttApplicationMessageRouteContext(
                scope.ServiceProvider,
                eventArgs,
                mqttServer,
                routeValues,
                route.RouteModel,
                _routingOptions);

            try
            {
                await route.InvokeAsync(context).ConfigureAwait(false);
                return new MqttApplicationMessageDispatchResult(true, context.ModelState);
            }
            catch (MqttBindingException ex)
            {
                _logger?.LogDebug(
                    ex,
                    "MQTT server message binding failed for topic '{Topic}' with {ErrorCount} model state error(s).",
                    eventArgs.ApplicationMessage.Topic,
                    ex.ModelState.ErrorCount);
                return new MqttApplicationMessageDispatchResult(false, ex.ModelState);
            }
        }
    }
}
