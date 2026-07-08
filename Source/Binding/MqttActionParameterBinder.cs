using MQTTnet;
using MQTTnet.AspNetCore.Routing.Attributes;
using MQTTnet.Packets;
using MQTTnet.Server;
using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace MQTTnet.AspNetCore.Routing
{
    /// <summary>
    /// 负责按 R3 binding source 将 MQTT action 参数绑定为 CLR 参数值。
    /// </summary>
    internal sealed class MqttActionParameterBinder
    {
        private readonly MqttRoutingOptions _options;

        /// <summary>
        /// 创建 action 参数绑定器。
        /// </summary>
        /// <param name="options">MQTT routing 配置和 formatter 集合。</param>
        public MqttActionParameterBinder(MqttRoutingOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        /// <summary>
        /// 绑定 action 的全部参数，失败时写入 model state 并抛出 <see cref="MqttBindingException"/>。
        /// </summary>
        /// <param name="parameters">action 参数列表。</param>
        /// <param name="actionContext">当前 MQTT action 上下文。</param>
        public async ValueTask<object?[]> BindAsync(
            ParameterInfo[] parameters,
            MqttActionContext actionContext)
        {
            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            if (actionContext == null)
            {
                throw new ArgumentNullException(nameof(actionContext));
            }

            var values = new object?[parameters.Length];
            for (var i = 0; i < parameters.Length; i++)
            {
                values[i] = await BindParameterAsync(parameters[i], actionContext).ConfigureAwait(false);
            }

            return values;
        }

        private ValueTask<object?> BindParameterAsync(ParameterInfo parameter, MqttActionContext actionContext)
        {
            var source = GetBindingSource(parameter, actionContext.RouteContext.RouteValues);
            switch (source)
            {
                case MqttBindingSource.Payload:
                    return BindPayloadAsync(parameter, actionContext);
                case MqttBindingSource.Session:
                    return ValueTask.FromResult(BindSession(parameter, actionContext));
                case MqttBindingSource.Client:
                    return ValueTask.FromResult(BindClient(parameter, actionContext));
                case MqttBindingSource.UserProperty:
                    return ValueTask.FromResult(BindUserProperty(parameter, actionContext));
                case MqttBindingSource.Context:
                    return ValueTask.FromResult(BindContext(parameter, actionContext));
                case MqttBindingSource.Route:
                case MqttBindingSource.Unknown:
                case MqttBindingSource.Services:
                default:
                    return ValueTask.FromResult(BindRoute(parameter, actionContext));
            }
        }

        private async ValueTask<object?> BindPayloadAsync(ParameterInfo parameter, MqttActionContext actionContext)
        {
            if (BindContextType(parameter.ParameterType, actionContext, out var contextValue))
            {
                return contextValue;
            }

            var attribute = parameter.GetCustomAttribute<FromMqttPayloadAttribute>(inherit: false);
            var jsonTypeInfo = ResolveJsonTypeInfo(parameter.ParameterType);
            var formatterContext = new MqttPayloadInputFormatterContext(
                actionContext,
                parameter.ParameterType,
                jsonTypeInfo,
                attribute?.ContentType,
                attribute?.FormatterName);

            var formatter = _options.InputFormatters.FirstOrDefault(item => item.CanRead(formatterContext));
            if (formatter == null)
            {
                actionContext.ModelState.AddModelError(
                    parameter.Name ?? "$payload",
                    MqttBindingErrorCode.PayloadFormatterNotFound,
                    "No MQTT payload formatter can read the target parameter type.");
                throw new MqttBindingException(
                    actionContext.ModelState,
                    $"No MQTT payload formatter can read parameter '{parameter.Name}'.");
            }

            return await formatter.ReadAsync(formatterContext).ConfigureAwait(false);
        }

        private object? BindRoute(ParameterInfo parameter, MqttActionContext actionContext)
        {
            var attribute = parameter.GetCustomAttribute<FromMqttRouteAttribute>(inherit: false);
            var key = ResolveKey(attribute?.Name, parameter);

            if (!actionContext.RouteContext.RouteValues.TryGetValue(key, out var value))
            {
                if (TryGetParameterDefaultValue(parameter, out var defaultValue))
                {
                    return defaultValue;
                }

                actionContext.ModelState.AddModelError(
                    key,
                    MqttBindingErrorCode.MissingRouteValue,
                    "Route value is required.");
                throw new MqttBindingException(
                    actionContext.ModelState,
                    $"No matching route value for parameter '{parameter.Name}'.");
            }

            return ConvertOrThrow(
                value,
                parameter.ParameterType,
                key,
                parameter,
                actionContext,
                MqttBindingErrorCode.TypeConversionFailed,
                "Route value conversion failed.");
        }

        private object? BindSession(ParameterInfo parameter, MqttActionContext actionContext)
        {
            if (typeof(IDictionary).IsAssignableFrom(parameter.ParameterType))
            {
                return actionContext.RequestContext.SessionItems;
            }

            var attribute = parameter.GetCustomAttribute<FromMqttSessionAttribute>(inherit: false);
            var key = ResolveKey(attribute?.Key, parameter);

            if (!actionContext.RequestContext.SessionItems.Contains(key))
            {
                if (TryGetParameterDefaultValue(parameter, out var defaultValue))
                {
                    return defaultValue;
                }

                actionContext.ModelState.AddModelError(
                    key,
                    MqttBindingErrorCode.MissingSessionItem,
                    "Session item is required.");
                throw new MqttBindingException(
                    actionContext.ModelState,
                    $"No matching MQTT session item for parameter '{parameter.Name}'.");
            }

            var value = actionContext.RequestContext.SessionItems[key];
            return ConvertOrThrow(
                value,
                parameter.ParameterType,
                key,
                parameter,
                actionContext,
                MqttBindingErrorCode.TypeConversionFailed,
                "Session item conversion failed.");
        }

        private object? BindClient(ParameterInfo parameter, MqttActionContext actionContext)
        {
            var attribute = parameter.GetCustomAttribute<FromMqttClientAttribute>(inherit: false);
            var name = string.IsNullOrWhiteSpace(attribute?.Name) ? "clientId" : attribute!.Name!;
            object? value;

            if (string.Equals(name, "clientId", StringComparison.OrdinalIgnoreCase))
            {
                value = actionContext.RequestContext.ClientId;
            }
            else if (string.Equals(name, "userName", StringComparison.OrdinalIgnoreCase))
            {
                value = actionContext.RequestContext.UserName;
            }
            else
            {
                actionContext.ModelState.AddModelError(
                    name,
                    MqttBindingErrorCode.UnsupportedBindingSource,
                    "The requested MQTT client value is not supported.");
                throw new MqttBindingException(
                    actionContext.ModelState,
                    $"MQTT client value '{name}' is not supported.");
            }

            if (value == null)
            {
                if (TryGetParameterDefaultValue(parameter, out var defaultValue))
                {
                    return defaultValue;
                }

                actionContext.ModelState.AddModelError(
                    name,
                    MqttBindingErrorCode.MissingClientValue,
                    "MQTT client value is required.");
                throw new MqttBindingException(
                    actionContext.ModelState,
                    $"MQTT client value '{name}' is required.");
            }

            return ConvertOrThrow(
                value,
                parameter.ParameterType,
                name,
                parameter,
                actionContext,
                MqttBindingErrorCode.TypeConversionFailed,
                "MQTT client value conversion failed.");
        }

        private object? BindUserProperty(ParameterInfo parameter, MqttActionContext actionContext)
        {
            if (parameter.ParameterType == typeof(IReadOnlyList<MqttUserProperty>)
                || parameter.ParameterType == typeof(IEnumerable<MqttUserProperty>))
            {
                return actionContext.RequestContext.UserProperties;
            }

            var attribute = parameter.GetCustomAttribute<FromMqttUserPropertyAttribute>(inherit: false);
            var key = ResolveKey(attribute?.Name, parameter);
            var values = actionContext.RequestContext.GetUserProperties(key);

            if (parameter.ParameterType == typeof(string[]))
            {
                return values.ToArray();
            }

            if (parameter.ParameterType == typeof(IReadOnlyList<string>)
                || parameter.ParameterType == typeof(IEnumerable<string>))
            {
                return values;
            }

            if (values.Count == 0)
            {
                if (TryGetParameterDefaultValue(parameter, out var defaultValue))
                {
                    return defaultValue;
                }

                actionContext.ModelState.AddModelError(
                    key,
                    MqttBindingErrorCode.MissingUserProperty,
                    "MQTT user property is required.");
                throw new MqttBindingException(
                    actionContext.ModelState,
                    $"No matching MQTT user property for parameter '{parameter.Name}'.");
            }

            return ConvertOrThrow(
                values[0],
                parameter.ParameterType,
                key,
                parameter,
                actionContext,
                MqttBindingErrorCode.TypeConversionFailed,
                "MQTT user property conversion failed.");
        }

        private object? BindContext(ParameterInfo parameter, MqttActionContext actionContext)
        {
            if (BindContextType(parameter.ParameterType, actionContext, out var value))
            {
                return value;
            }

            if (TryGetParameterDefaultValue(parameter, out var defaultValue))
            {
                return defaultValue;
            }

            var key = parameter.Name ?? "$context";
            actionContext.ModelState.AddModelError(
                key,
                MqttBindingErrorCode.UnsupportedBindingSource,
                "The target parameter type cannot be bound from MQTT context.");
            throw new MqttBindingException(
                actionContext.ModelState,
                $"Parameter '{parameter.Name}' cannot be bound from MQTT context.");
        }

        private static bool BindContextType(Type parameterType, MqttActionContext actionContext, out object? value)
        {
            if (parameterType == typeof(MqttActionContext))
            {
                value = actionContext;
                return true;
            }

            if (parameterType == typeof(MqttRequestContext))
            {
                value = actionContext.RequestContext;
                return true;
            }

            if (parameterType == typeof(MqttRouteContext))
            {
                value = actionContext.RouteContext;
                return true;
            }

            if (parameterType == typeof(MqttModelStateDictionary))
            {
                value = actionContext.ModelState;
                return true;
            }

            if (parameterType == typeof(MqttApplicationMessage))
            {
                value = actionContext.RequestContext.Message;
                return true;
            }

            if (parameterType == typeof(CancellationToken))
            {
                value = actionContext.RequestContext.CancellationToken;
                return true;
            }

            if (parameterType == typeof(IServiceProvider))
            {
                value = actionContext.RequestServices;
                return true;
            }

            if (parameterType == typeof(MqttServer))
            {
                value = actionContext.MqttServer;
                return actionContext.MqttServer != null;
            }

            value = null;
            return false;
        }

        private MqttBindingSource GetBindingSource(
            ParameterInfo parameter,
            IReadOnlyDictionary<string, object?> routeValues)
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
                || IsContextType(parameter.ParameterType))
            {
                return MqttBindingSource.Context;
            }

            if (parameter.Name != null && routeValues.ContainsKey(parameter.Name))
            {
                return MqttBindingSource.Route;
            }

            return MqttBindingSource.Route;
        }

        private static bool IsContextType(Type parameterType)
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

        private object? ConvertOrThrow(
            object? value,
            Type targetType,
            string key,
            ParameterInfo parameter,
            MqttActionContext actionContext,
            MqttBindingErrorCode errorCode,
            string fallbackErrorMessage)
        {
            if (value == null && TryGetParameterDefaultValue(parameter, out var defaultValue))
            {
                return defaultValue;
            }

            if (!MqttRouteValueConverter.TryConvert(
                    value,
                    targetType,
                    out var convertedValue,
                    out var errorMessage))
            {
                actionContext.ModelState.AddModelError(
                    key,
                    errorCode,
                    errorMessage ?? fallbackErrorMessage);
                throw new MqttBindingException(
                    actionContext.ModelState,
                    $"Cannot bind value to parameter '{parameter.Name}'.");
            }

            return convertedValue;
        }

        private JsonTypeInfo? ResolveJsonTypeInfo(Type parameterType)
        {
            var jsonTypeInfo = _options.SerializerContext?.GetTypeInfo(parameterType);
            if (jsonTypeInfo != null)
            {
                return jsonTypeInfo;
            }

            try
            {
                return _options.SerializerOptions?.GetTypeInfo(parameterType);
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is NotSupportedException)
            {
                return null;
            }
        }

        private static string ResolveKey(string? declaredName, ParameterInfo parameter)
        {
            if (!string.IsNullOrWhiteSpace(declaredName))
            {
                return declaredName!;
            }

            if (!string.IsNullOrWhiteSpace(parameter.Name))
            {
                return parameter.Name!;
            }

            throw new InvalidOperationException($"Parameter '{parameter.ParameterType.FullName}' does not have a name.");
        }

        private static bool TryGetParameterDefaultValue(ParameterInfo parameter, out object? defaultValue)
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
    }
}
