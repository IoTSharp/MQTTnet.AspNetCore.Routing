// Copyright (c) Atlas Lift Tech Inc. All rights reserved.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MQTTnet.AspNetCore.Routing.Attributes;
using MQTTnet.Server;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading.Tasks;

#nullable enable

namespace MQTTnet.AspNetCore.Routing
{
    internal class MqttRouter
    {
        private readonly ILogger<MqttRouter> logger;
        private readonly MqttRouteTable routeTable;
        private readonly ITypeActivatorCache typeActivator;
        private readonly MqttFilterProvider filterProvider;
        private readonly MqttFilterPipeline filterPipeline;
        private readonly MqttRoutingOptions options;

        public MqttServer? Server { get; set; }

        public MqttRouter(
            ILogger<MqttRouter> logger,
            MqttRouteTable routeTable,
            ITypeActivatorCache typeActivator,
            MqttFilterProvider filterProvider,
            MqttFilterPipeline filterPipeline,
            MqttRoutingOptions options)
        {
            this.logger = logger;
            this.routeTable = routeTable;
            this.typeActivator = typeActivator;
            this.filterProvider = filterProvider;
            this.filterPipeline = filterPipeline;
            this.options = options;
        }

        internal async Task<MqttRouteInvocationResult> OnIncomingApplicationMessage(IServiceProvider svcProvider, InterceptingPublishEventArgs context, bool allowUnmatchedRoutes)
        {
            if (context.SessionItems?.Contains(MqttRoutingInternal.ResultPublishSessionItemKey) == true)
            {
                return MqttRouteInvocationResult.Ignored();
            }

            // Don't process messages sent from the server itself. This avoids footguns like a server failing to publish
            // a message because a route isn't found on a controller.
            if (context.ClientId == null)
            {
                return MqttRouteInvocationResult.Ignored();
            }

            var routeContext = new MqttRouteMatchContext(context.ApplicationMessage.Topic);

            routeTable.Route(routeContext);

            if (routeContext.Handler == null)
            {
                // Route not found
                if (!allowUnmatchedRoutes)
                {
                    logger.LogDebug($"Rejecting message publish because '{context.ApplicationMessage.Topic}' did not match any known routes.");
                }

                context.ProcessPublish = allowUnmatchedRoutes;
                return MqttRouteInvocationResult.Unmatched();
            }
            else
            {
                using (var scope = svcProvider.CreateScope())
                {
                    Type? declaringType = routeContext.ControllerType;

                    if (declaringType == null)
                    {
                        throw new InvalidOperationException($"{routeContext.Handler} must have a declaring type.");
                    }

                    var classInstance = typeActivator.CreateInstance<object>(scope.ServiceProvider, declaringType);

                    var activateProperties = routeContext.ControllerContextProperties;

                    if (activateProperties.Length == 0)
                    {
                        logger.LogDebug($"MqttController '{declaringType.FullName}' does not have a property that can accept a controller context.  You may want to add a [{nameof(MqttControllerContextAttribute)}] to a pubilc property.");
                    }

                    var controllerContext = new MqttControllerContext()
                    {
                        MqttContext = context,
                        MqttServer = Server
                    };
                    using var loggerScope = logger.BeginScope(new Dictionary<string, object?>
                    {
                        ["mqtt.client_id"] = context.ClientId,
                        ["mqtt.topic"] = context.ApplicationMessage.Topic,
                        ["mqtt.route"] = routeContext.RouteModel?.Template,
                        ["mqtt.action"] = routeContext.Handler?.Name
                    });
                    var actionContext = new MqttActionContext(
                        MqttRequestContext.FromInterceptingPublish(context),
                        new MqttRouteContext(
                            routeContext.RouteModel,
                            MqttRouteContext.ToRouteValues(routeContext.Parameters)),
                        controllerContext.ModelState,
                        scope.ServiceProvider,
                        loggerScope,
                        Server,
                        context,
                        options);
                    controllerContext.ActionContext = actionContext;

                    for (int i = 0; i < activateProperties.Length; i++)
                    {
                        PropertyInfo property = activateProperties[i];
                        property.SetValue(classInstance, controllerContext);
                    }

                    if (routeContext.HaveControllerParameter)
                    {
                        if (!TryBindControllerRouteProperties(
                                declaringType,
                                classInstance,
                                routeContext,
                                controllerContext))
                        {
                            logger.LogDebug(
                                "MQTT controller route property binding failed for topic '{Topic}' with {ErrorCount} model state error(s).",
                                context.ApplicationMessage.Topic,
                                controllerContext.ModelState.ErrorCount);

                            var filters = filterProvider.GetFilters(actionContext, scope.ServiceProvider);
                            await filterPipeline
                                .ExecuteResultAsync(
                                    actionContext,
                                    filters,
                                    new MqttRejectResult(MQTTnet.Protocol.MqttPubAckReasonCode.PayloadFormatInvalid))
                                .ConfigureAwait(false);
                            return MqttRouteInvocationResult.Matched(
                                routeContext.RouteModel,
                                routeContext.Parameters,
                                controllerContext.ModelState);
                        }
                    }
                    var handler = routeContext.Handler ?? throw new InvalidOperationException("Matched MQTT route does not have an action handler.");

                    context.ProcessPublish = true;
                    var routeFilters = filterProvider.GetFilters(actionContext, scope.ServiceProvider);
                    await filterPipeline
                        .InvokeAsync(actionContext, routeFilters, classInstance, handler)
                        .ConfigureAwait(false);
                    return MqttRouteInvocationResult.Matched(
                        routeContext.RouteModel,
                        routeContext.Parameters,
                        actionContext.ModelState);
                }
            }
        }

        private static bool TryBindControllerRouteProperties(
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
            Type declaringType,
            object classInstance,
            MqttRouteMatchContext routeContext,
            MqttControllerContext controllerContext)
        {
            if (routeContext.ControllerTemplate == null ||
                routeContext.Parameters == null ||
                routeContext.ControllerRoutePropertyBinders.Length == 0)
            {
                return true;
            }

            var binders = routeContext.ControllerRoutePropertyBinders;
            for (var i = 0; i < binders.Length; i++)
            {
                var binder = binders[i];
                if (!routeContext.Parameters.TryGetValue(binder.RouteValueName, out var routeValue))
                {
                    continue;
                }

                if (!MqttRouteValueConverter.TryConvert(
                        routeValue,
                        binder.PropertyType,
                        out var convertedValue,
                        out var errorMessage))
                {
                    controllerContext.ModelState.AddModelError(
                        binder.RouteValueName,
                        MqttBindingErrorCode.TypeConversionFailed,
                        errorMessage ?? "Route value conversion failed.");
                    return false;
                }

                binder.SetValue(classInstance, convertedValue);
            }

            return true;
        }

    }
}
