// Copyright (c) Atlas Lift Tech Inc. All rights reserved.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MQTTnet.AspNetCore.Routing.Attributes;
using MQTTnet.Server;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
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
        private readonly MqttActionParameterBinder parameterBinder;
        private readonly MqttActionResultExecutor actionResultExecutor;
        private readonly MqttRoutingOptions options;

        public MqttServer? Server { get; set; }

        public MqttRouter(
            ILogger<MqttRouter> logger,
            MqttRouteTable routeTable,
            ITypeActivatorCache typeActivator,
            MqttActionParameterBinder parameterBinder,
            MqttActionResultExecutor actionResultExecutor,
            MqttRoutingOptions options)
        {
            this.logger = logger;
            this.routeTable = routeTable;
            this.typeActivator = typeActivator;
            this.parameterBinder = parameterBinder;
            this.actionResultExecutor = actionResultExecutor;
            this.options = options;
        }

        internal async Task OnIncomingApplicationMessage(IServiceProvider svcProvider, InterceptingPublishEventArgs context, bool allowUnmatchedRoutes)
        {
            if (context.SessionItems?.Contains(MqttRoutingInternal.ResultPublishSessionItemKey) == true)
            {
                return;
            }

            // Don't process messages sent from the server itself. This avoids footguns like a server failing to publish
            // a message because a route isn't found on a controller.
            if (context.ClientId == null)
            {
                return;
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

                    // Potential perf improvement is to cache this reflection work in the future.
                    var activateProperties = declaringType.GetRuntimeProperties()
                        .Where((property) =>
                        {
                            return
                                property.IsDefined(typeof(MqttControllerContextAttribute)) &&
                                property.GetIndexParameters().Length == 0 &&
                                property.SetMethod != null &&
                                !property.SetMethod.IsStatic;
                        })
                        .ToArray();

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

                            context.ProcessPublish = false;
                            return;
                        }
                    }
                    var handler = routeContext.Handler ?? throw new InvalidOperationException("Matched MQTT route does not have an action handler.");
                    ParameterInfo[] parameters = handler.GetParameters();

                    context.ProcessPublish = true;

                    try
                    {
                        object?[]? paramArray = null;

                        if (parameters.Length > 0)
                        {
                            paramArray = await parameterBinder
                                .BindAsync(parameters, actionContext)
                                .ConfigureAwait(false);
                        }

                        var returnValue = HandlerInvoker(handler, classInstance, paramArray);
                        await actionResultExecutor
                            .ExecuteAsync(handler.ReturnType, returnValue, actionContext)
                            .ConfigureAwait(false);
                    }
                    catch (MqttBindingException ex)
                    {
                        logger.LogDebug(
                            ex,
                            "MQTT action binding failed for topic '{Topic}' with {ErrorCount} model state error(s).",
                            context.ApplicationMessage.Topic,
                            ex.ModelState.ErrorCount);

                        context.ProcessPublish = false;
                    }
                    catch (ArgumentException ex)
                    {
                        logger.LogError(ex, $"Unable to match route parameters to all arguments. See inner exception for details.");

                        context.ProcessPublish = false;
                    }
                    catch (TargetInvocationException ex) when (ex.InnerException is MqttBindingException bindingException)
                    {
                        logger.LogDebug(
                            bindingException,
                            "MQTT action binding failed for topic '{Topic}' with {ErrorCount} model state error(s).",
                            context.ApplicationMessage.Topic,
                            bindingException.ModelState.ErrorCount);

                        context.ProcessPublish = false;
                    }
                    catch (TargetInvocationException ex)
                    {
                        logger.LogError(ex.InnerException, $"Unhandled MQTT action exception. See inner exception for details.");

                        // This is an unandled exception from the invoked action
                        context.ProcessPublish = false;
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Unable to invoke Mqtt Action.  See inner exception for details.");

                        context.ProcessPublish = false;
                    }
                }
            }
        }

        private static object? HandlerInvoker(MethodInfo method, object instance, object?[]? parameters)
        {
            return method.Invoke(instance, parameters);
        }

        private static bool TryBindControllerRouteProperties(
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
            Type declaringType,
            object classInstance,
            MqttRouteMatchContext routeContext,
            MqttControllerContext controllerContext)
        {
            if (routeContext.ControllerTemplate == null || routeContext.Parameters == null)
            {
                return true;
            }

            foreach (var templateSegment in routeContext.ControllerTemplate.Segments.Where(p => p.IsParameter))
            {
                var property = declaringType.GetRuntimeProperty(templateSegment.Value);
                if (property?.SetMethod == null ||
                    !routeContext.Parameters.TryGetValue(templateSegment.Value, out var routeValue))
                {
                    continue;
                }

                if (!MqttRouteValueConverter.TryConvert(
                        routeValue,
                        property.PropertyType,
                        out var convertedValue,
                        out var errorMessage))
                {
                    controllerContext.ModelState.AddModelError(
                        templateSegment.Value,
                        MqttBindingErrorCode.TypeConversionFailed,
                        errorMessage ?? "Route value conversion failed.");
                    return false;
                }

                property.SetValue(classInstance, convertedValue);
            }

            return true;
        }

    }
}
