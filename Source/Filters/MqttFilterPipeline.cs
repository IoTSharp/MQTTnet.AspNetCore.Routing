using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

#nullable enable

namespace MQTTnet.AspNetCore.Routing
{
    /// <summary>
    /// 执行 MQTT controller 路径的 authorization、resource、action、exception 与 result filter 管线。
    /// </summary>
    internal sealed class MqttFilterPipeline
    {
        private readonly ILogger<MqttFilterPipeline> _logger;
        private readonly MqttActionParameterBinder _parameterBinder;
        private readonly MqttActionResultExecutor _resultExecutor;

        /// <summary>
        /// 创建 MQTT filter 管线执行器。
        /// </summary>
        /// <param name="logger">日志记录器。</param>
        /// <param name="parameterBinder">action 参数绑定器。</param>
        /// <param name="resultExecutor">action result 执行器。</param>
        public MqttFilterPipeline(
            ILogger<MqttFilterPipeline> logger,
            MqttActionParameterBinder parameterBinder,
            MqttActionResultExecutor resultExecutor)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _parameterBinder = parameterBinder ?? throw new ArgumentNullException(nameof(parameterBinder));
            _resultExecutor = resultExecutor ?? throw new ArgumentNullException(nameof(resultExecutor));
        }

        /// <summary>
        /// 执行完整 filter 管线并调用 controller action。
        /// </summary>
        /// <param name="actionContext">当前 MQTT action 上下文。</param>
        /// <param name="filters">当前 action 关联的 filter。</param>
        /// <param name="controller">controller 实例。</param>
        /// <param name="actionMethod">action 方法。</param>
        public async ValueTask InvokeAsync(
            MqttActionContext actionContext,
            IReadOnlyList<IMqttFilterMetadata> filters,
            object controller,
            MethodInfo actionMethod)
        {
            if (actionContext == null)
            {
                throw new ArgumentNullException(nameof(actionContext));
            }

            if (filters == null)
            {
                throw new ArgumentNullException(nameof(filters));
            }

            if (controller == null)
            {
                throw new ArgumentNullException(nameof(controller));
            }

            if (actionMethod == null)
            {
                throw new ArgumentNullException(nameof(actionMethod));
            }

            var result = await InvokeAuthorizationFiltersAsync(actionContext, filters).ConfigureAwait(false);
            if (result == null)
            {
                result = await InvokeExceptionFiltersAsync(
                        actionContext,
                        filters,
                        () => InvokeResourcePipelineAsync(actionContext, filters, controller, actionMethod))
                    .ConfigureAwait(false);
            }

            if (result == null)
            {
                if (actionContext.InterceptingPublishContext != null)
                {
                    actionContext.InterceptingPublishContext.ProcessPublish = false;
                }
                return;
            }

            await ExecuteResultWithExceptionFiltersAsync(actionContext, filters, result).ConfigureAwait(false);
        }

        /// <summary>
        /// 在已有 action context 上执行 result 管线，用于绑定失败等 action 外结果。
        /// </summary>
        /// <param name="actionContext">当前 MQTT action 上下文。</param>
        /// <param name="filters">当前 action 关联的 filter。</param>
        /// <param name="result">要执行的 MQTT result。</param>
        public async ValueTask ExecuteResultAsync(
            MqttActionContext actionContext,
            IReadOnlyList<IMqttFilterMetadata> filters,
            MqttResult result)
        {
            await ExecuteResultWithExceptionFiltersAsync(actionContext, filters, result).ConfigureAwait(false);
        }

        private static async ValueTask<MqttResult?> InvokeAuthorizationFiltersAsync(
            MqttActionContext actionContext,
            IReadOnlyList<IMqttFilterMetadata> filters)
        {
            var authorizationContext = new MqttAuthorizationFilterContext(actionContext, filters);
            foreach (var filter in filters.OfType<IMqttAuthorizationFilter>())
            {
                await filter.OnAuthorizationAsync(authorizationContext).ConfigureAwait(false);
                if (authorizationContext.Result != null)
                {
                    return authorizationContext.Result;
                }
            }

            return null;
        }

        private async ValueTask<MqttResult?> InvokeExceptionFiltersAsync(
            MqttActionContext actionContext,
            IReadOnlyList<IMqttFilterMetadata> filters,
            Func<ValueTask<MqttResult>> action)
        {
            try
            {
                return await action().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                var exception = UnwrapInvocationException(ex);
                var exceptionContext = new MqttExceptionContext(actionContext, filters, exception);
                foreach (var filter in filters.OfType<IMqttExceptionFilter>())
                {
                    await filter.OnExceptionAsync(exceptionContext).ConfigureAwait(false);
                    if (exceptionContext.ExceptionHandled)
                    {
                        break;
                    }
                }

                if (exceptionContext.ExceptionHandled)
                {
                    return exceptionContext.Result ?? new MqttRejectResult();
                }

                _logger.LogError(exception, "Unhandled MQTT route exception.");
                if (actionContext.InterceptingPublishContext != null)
                {
                    actionContext.InterceptingPublishContext.ProcessPublish = false;
                }

                return null;
            }
        }

        private async ValueTask ExecuteResultWithExceptionFiltersAsync(
            MqttActionContext actionContext,
            IReadOnlyList<IMqttFilterMetadata> filters,
            MqttResult result)
        {
            var handledResult = await InvokeExceptionFiltersAsync(
                    actionContext,
                    filters,
                    async () =>
                    {
                        await InvokeResultPipelineAsync(actionContext, filters, result).ConfigureAwait(false);
                        return MqttEmptyResult.Instance;
                    })
                .ConfigureAwait(false);

            if (handledResult != null && !ReferenceEquals(handledResult, MqttEmptyResult.Instance))
            {
                try
                {
                    await InvokeResultPipelineAsync(actionContext, filters, handledResult).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unhandled MQTT result exception.");
                    if (actionContext.InterceptingPublishContext != null)
                    {
                        actionContext.InterceptingPublishContext.ProcessPublish = false;
                    }
                }
            }
        }

        private async ValueTask<MqttResult> InvokeResourcePipelineAsync(
            MqttActionContext actionContext,
            IReadOnlyList<IMqttFilterMetadata> filters,
            object controller,
            MethodInfo actionMethod)
        {
            MqttResourceExecutionDelegate next = () => InvokeActionAsResourceAsync(
                actionContext,
                filters,
                controller,
                actionMethod);

            foreach (var filter in filters.OfType<IMqttResourceFilter>().Reverse())
            {
                var nextCopy = next;
                var current = filter;
                next = () =>
                {
                    var executingContext = new MqttResourceExecutingContext(
                        actionContext,
                        filters,
                        controller,
                        actionMethod);
                    if (executingContext.Result != null)
                    {
                        return ValueTask.FromResult(new MqttResourceExecutedContext(
                            actionContext,
                            filters,
                            controller,
                            actionMethod,
                            executingContext.Result,
                            exception: null,
                            canceled: true));
                    }

                    return current.OnResourceExecutionAsync(executingContext, nextCopy);
                };
            }

            var executed = await next().ConfigureAwait(false);
            if (executed.Exception != null && !executed.ExceptionHandled)
            {
                throw executed.Exception;
            }

            return executed.Result ?? MqttEmptyResult.Instance;
        }

        private async ValueTask<MqttResourceExecutedContext> InvokeActionAsResourceAsync(
            MqttActionContext actionContext,
            IReadOnlyList<IMqttFilterMetadata> filters,
            object controller,
            MethodInfo actionMethod)
        {
            var actionExecuted = await InvokeActionPipelineAsync(
                    actionContext,
                    filters,
                    controller,
                    actionMethod)
                .ConfigureAwait(false);

            if (actionExecuted.Exception != null && !actionExecuted.ExceptionHandled)
            {
                return new MqttResourceExecutedContext(
                    actionContext,
                    filters,
                    controller,
                    actionMethod,
                    actionExecuted.Result,
                    actionExecuted.Exception,
                    canceled: actionExecuted.Canceled);
            }

            return new MqttResourceExecutedContext(
                actionContext,
                filters,
                controller,
                actionMethod,
                actionExecuted.Result,
                exception: null,
                canceled: actionExecuted.Canceled);
        }

        private async ValueTask<MqttActionExecutedContext> InvokeActionPipelineAsync(
            MqttActionContext actionContext,
            IReadOnlyList<IMqttFilterMetadata> filters,
            object controller,
            MethodInfo actionMethod)
        {
            var parameters = actionMethod.GetParameters();
            object?[]? actionArguments = null;
            if (parameters.Length > 0)
            {
                actionArguments = await _parameterBinder
                    .BindAsync(parameters, actionContext)
                    .ConfigureAwait(false);
            }

            var executingContext = new MqttActionExecutingContext(
                actionContext,
                filters,
                controller,
                actionMethod,
                actionArguments);
            MqttActionExecutionDelegate next = () => InvokeActionCoreAsync(executingContext);

            foreach (var filter in filters.OfType<IMqttActionFilter>().Reverse())
            {
                var nextCopy = next;
                var current = filter;
                next = () => current.OnActionExecutionAsync(executingContext, nextCopy);
            }

            return await next().ConfigureAwait(false);
        }

        private async ValueTask<MqttActionExecutedContext> InvokeActionCoreAsync(
            MqttActionExecutingContext executingContext)
        {
            if (executingContext.Result != null)
            {
                return new MqttActionExecutedContext(
                    executingContext.ActionContext,
                    executingContext.Filters,
                    executingContext.Controller,
                    executingContext.ActionMethod,
                    executingContext.ActionArguments.ToArray(),
                    executingContext.Result,
                    exception: null,
                    canceled: true);
            }

            try
            {
                var returnValue = executingContext.ActionMethod.Invoke(
                    executingContext.Controller,
                    executingContext.ActionArguments.ToArray());
                var result = await _resultExecutor
                    .CreateResultAsync(executingContext.ActionMethod.ReturnType, returnValue, executingContext.ActionContext)
                    .ConfigureAwait(false);
                return new MqttActionExecutedContext(
                    executingContext.ActionContext,
                    executingContext.Filters,
                    executingContext.Controller,
                    executingContext.ActionMethod,
                    executingContext.ActionArguments.ToArray(),
                    result,
                    exception: null,
                    canceled: false);
            }
            catch (Exception ex)
            {
                return new MqttActionExecutedContext(
                    executingContext.ActionContext,
                    executingContext.Filters,
                    executingContext.Controller,
                    executingContext.ActionMethod,
                    executingContext.ActionArguments.ToArray(),
                    result: null,
                    exception: UnwrapInvocationException(ex),
                    canceled: false);
            }
        }

        private async ValueTask InvokeResultPipelineAsync(
            MqttActionContext actionContext,
            IReadOnlyList<IMqttFilterMetadata> filters,
            MqttResult result)
        {
            var executingContext = new MqttResultExecutingContext(actionContext, filters, result);
            MqttResultExecutionDelegate next = () => ExecuteResultCoreAsync(executingContext);

            foreach (var filter in filters.OfType<IMqttResultFilter>().Reverse())
            {
                var nextCopy = next;
                var current = filter;
                next = () => current.OnResultExecutionAsync(executingContext, nextCopy);
            }

            var executed = await next().ConfigureAwait(false);
            if (executed.Exception != null && !executed.ExceptionHandled)
            {
                throw executed.Exception;
            }
        }

        private async ValueTask<MqttResultExecutedContext> ExecuteResultCoreAsync(
            MqttResultExecutingContext executingContext)
        {
            if (executingContext.Cancel)
            {
                return new MqttResultExecutedContext(
                    executingContext.ActionContext,
                    executingContext.Filters,
                    executingContext.Result,
                    exception: null,
                    canceled: true);
            }

            try
            {
                await _resultExecutor
                    .ExecuteResultAsync(executingContext.Result, executingContext.ActionContext)
                    .ConfigureAwait(false);
                return new MqttResultExecutedContext(
                    executingContext.ActionContext,
                    executingContext.Filters,
                    executingContext.Result,
                    exception: null,
                    canceled: false);
            }
            catch (Exception ex)
            {
                return new MqttResultExecutedContext(
                    executingContext.ActionContext,
                    executingContext.Filters,
                    executingContext.Result,
                    UnwrapInvocationException(ex),
                    canceled: false);
            }
        }

        private static Exception UnwrapInvocationException(Exception exception)
        {
            return exception is TargetInvocationException { InnerException: { } innerException }
                ? innerException
                : exception;
        }
    }
}
