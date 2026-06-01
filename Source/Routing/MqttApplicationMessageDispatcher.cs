// Copyright (c) Atlas Lift Tech Inc. All rights reserved.

using Microsoft.Extensions.DependencyInjection;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace MQTTnet.AspNetCore.Routing
{
    internal sealed class MqttApplicationMessageDispatcher : IMqttApplicationMessageDispatcher
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly MqttApplicationMessageRouteTable _routeTable;

        public MqttApplicationMessageDispatcher(
            IServiceScopeFactory scopeFactory,
            MqttApplicationMessageRouteTable routeTable)
        {
            _scopeFactory = scopeFactory;
            _routeTable = routeTable;
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
                cancellationToken);

            await route.InvokeAsync(context).ConfigureAwait(false);
            return new MqttApplicationMessageDispatchResult(true);
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
    }
}
