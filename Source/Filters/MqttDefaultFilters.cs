using Microsoft.Extensions.Logging;
using MQTTnet.Protocol;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading.Tasks;

#nullable enable

namespace MQTTnet.AspNetCore.Routing
{
    /// <summary>
    /// 在配置了 payload 大小上限时拒绝超限消息。
    /// </summary>
    public sealed class MqttPayloadSizeLimitFilter : IMqttAuthorizationFilter, IOrderedMqttFilter
    {
        /// <inheritdoc />
        public int Order => int.MinValue + 100;

        /// <inheritdoc />
        public ValueTask OnAuthorizationAsync(MqttAuthorizationFilterContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var limit = context.ActionContext.RoutingOptions?.MaxPayloadSizeBytes;
            if (!limit.HasValue || limit.Value < 0)
            {
                return ValueTask.CompletedTask;
            }

            if (context.Message.Payload.Length > limit.Value)
            {
                context.Result = new MqttRejectResult(
                    MqttPubAckReasonCode.QuotaExceeded,
                    "MQTT payload exceeds the configured size limit.");
            }

            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// 当 model state 已无效时拒绝 action 执行。
    /// </summary>
    public sealed class MqttModelStateInvalidFilter : IMqttActionFilter, IOrderedMqttFilter
    {
        /// <inheritdoc />
        public int Order => int.MinValue + 200;

        /// <inheritdoc />
        public ValueTask<MqttActionExecutedContext> OnActionExecutionAsync(
            MqttActionExecutingContext context,
            MqttActionExecutionDelegate next)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (next == null)
            {
                throw new ArgumentNullException(nameof(next));
            }

            if (context.ModelState.IsValid)
            {
                return next();
            }

            var result = new MqttRejectResult(
                MqttPubAckReasonCode.PayloadFormatInvalid,
                "MQTT model state is invalid.");
            return ValueTask.FromResult(new MqttActionExecutedContext(
                context.ActionContext,
                context.Filters,
                context.Controller,
                context.ActionMethod,
                context.ActionArguments.ToArray(),
                result,
                exception: null,
                canceled: true));
        }
    }

    /// <summary>
    /// 将未处理异常转换为默认拒绝结果，保持历史默认语义。
    /// </summary>
    public sealed class MqttExceptionToResultFilter : IMqttExceptionFilter, IOrderedMqttFilter
    {
        /// <inheritdoc />
        public int Order => int.MaxValue - 100;

        /// <inheritdoc />
        public ValueTask OnExceptionAsync(MqttExceptionContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            context.ExceptionHandled = true;
            context.Result ??= new MqttRejectResult(MqttPubAckReasonCode.UnspecifiedError);
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// 为 MQTT route 执行创建日志作用域。
    /// </summary>
    public sealed class MqttLoggingScopeFilter : IMqttResourceFilter, IOrderedMqttFilter
    {
        private readonly ILogger<MqttLoggingScopeFilter> _logger;

        /// <summary>
        /// 创建日志作用域 filter。
        /// </summary>
        /// <param name="logger">日志记录器。</param>
        public MqttLoggingScopeFilter(ILogger<MqttLoggingScopeFilter> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public int Order => int.MinValue + 150;

        /// <inheritdoc />
        public async ValueTask<MqttResourceExecutedContext> OnResourceExecutionAsync(
            MqttResourceExecutingContext context,
            MqttResourceExecutionDelegate next)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (next == null)
            {
                throw new ArgumentNullException(nameof(next));
            }

            using (_logger.BeginScope(new Dictionary<string, object?>
            {
                ["mqtt.client_id"] = context.RequestContext.ClientId,
                ["mqtt.topic"] = context.RequestContext.Topic,
                ["mqtt.route"] = context.RouteContext.MatchedRoute?.Template,
                ["mqtt.action"] = context.ActionMethod.Name
            }))
            {
                return await next().ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// 记录 MQTT route 执行的通用指标。
    /// </summary>
    public sealed class MqttRoutingMetricsFilter : IMqttResourceFilter, IOrderedMqttFilter
    {
        private static readonly Meter Meter = new("MQTTnet.AspNetCore.Routing");
        private static readonly Counter<long> ActionStarted = Meter.CreateCounter<long>("mqtt.routing.action.started");
        private static readonly Counter<long> ActionFailed = Meter.CreateCounter<long>("mqtt.routing.action.failed");
        private static readonly Histogram<double> ActionDuration = Meter.CreateHistogram<double>("mqtt.routing.action.duration.ms");

        /// <inheritdoc />
        public int Order => int.MinValue + 250;

        /// <inheritdoc />
        public async ValueTask<MqttResourceExecutedContext> OnResourceExecutionAsync(
            MqttResourceExecutingContext context,
            MqttResourceExecutionDelegate next)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (next == null)
            {
                throw new ArgumentNullException(nameof(next));
            }

            var route = context.RouteContext.MatchedRoute?.Template ?? string.Empty;
            var action = context.ActionMethod.Name;
            var tags = new KeyValuePair<string, object?>[]
            {
                new("mqtt.route", route),
                new("mqtt.action", action)
            };
            var started = Stopwatch.GetTimestamp();
            ActionStarted.Add(1, tags);

            var executed = await next().ConfigureAwait(false);
            var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            ActionDuration.Record(elapsed, tags);

            if (executed.Exception != null && !executed.ExceptionHandled)
            {
                ActionFailed.Add(1, tags);
            }

            return executed;
        }
    }
}
