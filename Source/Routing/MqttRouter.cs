// Copyright (c) Atlas Lift Tech Inc. All rights reserved.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MQTTnet.AspNetCore.Routing.Attributes;
using MQTTnet.Server;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;

#nullable enable

namespace MQTTnet.AspNetCore.Routing
{
    internal class MqttRouter
    {
        private readonly ILogger<MqttRouter> logger;
        private readonly MqttRouteTable routeTable;
        private readonly ITypeActivatorCache typeActivator;

        public MqttServer? Server { get; set; }

        public MqttRouter(ILogger<MqttRouter> logger, MqttRouteTable routeTable, ITypeActivatorCache typeActivator)
        {
            this.logger = logger;
            this.routeTable = routeTable;
            this.typeActivator = typeActivator;
        }

        internal async Task OnIncomingApplicationMessage(IServiceProvider svcProvider, InterceptingPublishEventArgs context, bool allowUnmatchedRoutes)
        {
            // Don't process messages sent from the server itself. This avoids footguns like a server failing to publish
            // a message because a route isn't found on a controller.
            if (context.ClientId == null)
            {
                return;
            }

            var routeContext = new MqttRouteContext(context.ApplicationMessage.Topic);

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
                    ParameterInfo[] parameters = routeContext.Handler.GetParameters();

                    context.ProcessPublish = true;

                    if (parameters.Length == 0)
                    {
                        await HandlerInvoker(routeContext.Handler, classInstance, null).ConfigureAwait(false);
                    }
                    else
                    {
                        object?[] paramArray;

                        try
                        {
                            paramArray = parameters.Select(p =>
                                    MatchParameterOrThrow(p, routeContext.Parameters, controllerContext, svcProvider)
                                )
                                .ToArray();

                            await HandlerInvoker(routeContext.Handler, classInstance, paramArray).ConfigureAwait(false);
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
        }

        private static Task HandlerInvoker(MethodInfo method, object instance, object?[]? parameters)
        {
            if (method.ReturnType == typeof(void))
            {
                method.Invoke(instance, parameters);

                return Task.CompletedTask;
            }
            else if (method.ReturnType == typeof(Task))
            {
                var result = (Task?)method.Invoke(instance, parameters);

                if (result == null)
                {
                    throw new NullReferenceException($"{method.DeclaringType?.FullName ?? "<unknown>"}.{method.Name} returned null instead of Task");
                }

                return result;
            }

            throw new InvalidOperationException($"Unsupported Action return type \"{method.ReturnType}\" on method {method.DeclaringType?.FullName ?? "<unknown>"}.{method.Name}. Only void and {nameof(Task)} are allowed.");
        }

        private static bool TryBindControllerRouteProperties(
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
            Type declaringType,
            object classInstance,
            MqttRouteContext routeContext,
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

        private static object? MatchParameterOrThrow(ParameterInfo param,
            IReadOnlyDictionary<string, object> availableParmeters, MqttControllerContext controllerContext,
            IServiceProvider serviceProvider)
        {
            if (param.IsDefined(typeof(FromPayloadAttribute), false))
            {
                var routingOptions = serviceProvider.GetService<MqttRoutingOptions>();
                JsonTypeInfo? jsonTypeInfo = routingOptions?.SerializerContext?.GetTypeInfo(param.ParameterType);
                if (jsonTypeInfo != null)
                {
                    return DeserializePayloadOrThrow(param, controllerContext, jsonTypeInfo);
                }

                jsonTypeInfo = routingOptions?.SerializerOptions?.GetTypeInfo(param.ParameterType);
                if (jsonTypeInfo == null)
                {
                    throw new InvalidOperationException($"No JSON type metadata is configured for '{param.ParameterType.FullName}'.");
                }

                return DeserializePayloadOrThrow(param, controllerContext, jsonTypeInfo);
            }
            object? value = null;
            if (param.Name == null || availableParmeters == null || !availableParmeters.TryGetValue(param.Name, out value))
            {
                if (TryGetParameterDefaultValue(param, out var defaultValue))
                {
                    return defaultValue;
                }

                var key = param.Name ?? "$parameter";
                controllerContext.ModelState.AddModelError(
                    key,
                    MqttBindingErrorCode.MissingRouteValue,
                    "Route value is required.");
                throw new MqttBindingException(
                    controllerContext.ModelState,
                    $"No matching route parameter for \"{param.ParameterType.Name} {param.Name}\"");
            }

            if (value == null && TryGetParameterDefaultValue(param, out var optionalDefaultValue))
            {
                return optionalDefaultValue;
            }

            if (!MqttRouteValueConverter.TryConvert(
                    value,
                    param.ParameterType,
                    out var convertedValue,
                    out var routeValueError))
            {
                var key = param.Name ?? "$parameter";
                controllerContext.ModelState.AddModelError(
                    key,
                    MqttBindingErrorCode.TypeConversionFailed,
                    routeValueError ?? "Route value conversion failed.");
                throw new MqttBindingException(
                    controllerContext.ModelState,
                    $"Cannot assign route value to parameter \"{param.ParameterType.Name} {param.Name}\"");
            }

            return convertedValue;
        }

        private static object? DeserializePayloadOrThrow(
            ParameterInfo param,
            MqttControllerContext controllerContext,
            JsonTypeInfo jsonTypeInfo)
        {
            try
            {
                return MqttJsonPayloadSerializer.Deserialize(
                    controllerContext.MqttContext.ApplicationMessage.Payload,
                    jsonTypeInfo);
            }
            catch (JsonException ex)
            {
                var key = param.Name ?? "$payload";
                controllerContext.ModelState.AddModelError(
                    key,
                    MqttBindingErrorCode.PayloadDeserializationFailed,
                    "MQTT payload could not be deserialized.");
                throw new MqttBindingException(
                    controllerContext.ModelState,
                    "MQTT payload could not be deserialized.",
                    ex);
            }
        }

        private static bool TryGetParameterDefaultValue(ParameterInfo param, out object? defaultValue)
        {
            if (param.HasDefaultValue)
            {
                defaultValue = param.DefaultValue == DBNull.Value ? null : param.DefaultValue;
                return true;
            }

            if (param.IsOptional)
            {
                defaultValue = null;
                return true;
            }

            defaultValue = null;
            return false;
        }
    }
}
