// Copyright (c) Atlas Lift Tech Inc. All rights reserved.

using MQTTnet;
using System;
using System.Collections.Generic;
using System.Threading;

#nullable enable

namespace MQTTnet.AspNetCore.Routing
{
    public sealed class MqttApplicationMessageRouteContext
    {
        internal MqttApplicationMessageRouteContext(
            IServiceProvider services,
            MqttApplicationMessage message,
            string? clientId,
            IReadOnlyDictionary<string, string> routeValues,
            MqttRouteModel? routeModel,
            CancellationToken cancellationToken)
        {
            Services = services;
            Message = message;
            ClientId = clientId;
            RouteValues = routeValues;
            CancellationToken = cancellationToken;
            RequestContext = new MqttRequestContext(
                message,
                clientId,
                cancellationToken: cancellationToken);
            RouteContext = new MqttRouteContext(
                routeModel,
                MqttRouteContext.ToRouteValues(routeValues));
            ActionContext = new MqttActionContext(
                RequestContext,
                RouteContext,
                ModelState,
                services);
        }

        public IServiceProvider Services { get; }

        public MqttApplicationMessage Message { get; }

        public string? ClientId { get; }

        public IReadOnlyDictionary<string, string> RouteValues { get; }

        public CancellationToken CancellationToken { get; }

        /// <summary>
        /// 当前消息的 R3 请求上下文。
        /// </summary>
        public MqttRequestContext RequestContext { get; }

        /// <summary>
        /// 当前消息的 R3 route 上下文。
        /// </summary>
        public MqttRouteContext RouteContext { get; }

        /// <summary>
        /// 当前消息的 R3 action 上下文。
        /// </summary>
        public MqttActionContext ActionContext { get; }

        /// <summary>
        /// 当前消息路由和绑定过程产生的错误。
        /// </summary>
        public MqttModelStateDictionary ModelState { get; } = new MqttModelStateDictionary();

        /// <summary>
        /// 读取原始字符串 route value。
        /// </summary>
        /// <param name="key">route value 名称。</param>
        /// <returns>未转换的 route value。</returns>
        public string GetRouteValue(string key)
        {
            if (RouteValues.TryGetValue(key, out var value))
            {
                return value;
            }

            throw new KeyNotFoundException($"Route value '{key}' was not found.");
        }

        /// <summary>
        /// 读取并转换 route value，失败时记录 model state 并抛出绑定异常。
        /// </summary>
        /// <typeparam name="T">目标类型。</typeparam>
        /// <param name="key">route value 名称。</param>
        /// <returns>转换后的 route value。</returns>
        public T? GetRouteValue<T>(string key)
        {
            if (TryGetRouteValue<T>(key, out var value))
            {
                return value;
            }

            throw new MqttBindingException(ModelState, $"Route value '{key}' could not be bound.");
        }

        /// <summary>
        /// 尝试读取并转换 route value，失败时记录 model state。
        /// </summary>
        /// <typeparam name="T">目标类型。</typeparam>
        /// <param name="key">route value 名称。</param>
        /// <param name="value">转换后的 route value。</param>
        /// <returns>转换是否成功。</returns>
        public bool TryGetRouteValue<T>(string key, out T? value)
        {
            if (!RouteValues.TryGetValue(key, out var rawValue))
            {
                ModelState.AddModelError(
                    key,
                    MqttBindingErrorCode.MissingRouteValue,
                    "Route value is required.");
                value = default;
                return false;
            }

            if (!MqttRouteValueConverter.TryConvert(rawValue, typeof(T), out var convertedValue, out var errorMessage))
            {
                ModelState.AddModelError(
                    key,
                    MqttBindingErrorCode.TypeConversionFailed,
                    errorMessage ?? "Route value conversion failed.");
                value = default;
                return false;
            }

            value = (T?)convertedValue;
            return true;
        }
    }
}
