using MQTTnet;
using MQTTnet.Server;
using System;
using System.Collections.Generic;
using System.Reflection;

#nullable enable

namespace MQTTnet.AspNetCore.Routing
{
    /// <summary>
    /// MQTT filter 上下文基类。
    /// </summary>
    public abstract class MqttFilterContext
    {
        /// <summary>
        /// 创建 filter 上下文。
        /// </summary>
        /// <param name="actionContext">当前 MQTT action 上下文。</param>
        /// <param name="filters">当前 action 关联的全部 filter 实例。</param>
        protected MqttFilterContext(
            MqttActionContext actionContext,
            IReadOnlyList<IMqttFilterMetadata> filters)
        {
            ActionContext = actionContext ?? throw new ArgumentNullException(nameof(actionContext));
            Filters = filters ?? throw new ArgumentNullException(nameof(filters));
        }

        /// <summary>
        /// 当前 MQTT action 上下文。
        /// </summary>
        public MqttActionContext ActionContext { get; }

        /// <summary>
        /// 当前 action 关联的全部 filter 实例。
        /// </summary>
        public IReadOnlyList<IMqttFilterMetadata> Filters { get; }

        /// <summary>
        /// MQTT 请求上下文。
        /// </summary>
        public MqttRequestContext RequestContext => ActionContext.RequestContext;

        /// <summary>
        /// MQTT route 上下文。
        /// </summary>
        public MqttRouteContext RouteContext => ActionContext.RouteContext;

        /// <summary>
        /// 当前绑定错误集合。
        /// </summary>
        public MqttModelStateDictionary ModelState => ActionContext.ModelState;

        /// <summary>
        /// 当前请求作用域服务。
        /// </summary>
        public IServiceProvider RequestServices => ActionContext.RequestServices;

        /// <summary>
        /// MQTT server 实例；非 server 路径可为空。
        /// </summary>
        public MqttServer? MqttServer => ActionContext.MqttServer;

        /// <summary>
        /// MQTTnet server publish 拦截上下文；非 server 路径可为空。
        /// </summary>
        public InterceptingPublishEventArgs? InterceptingPublishContext => ActionContext.InterceptingPublishContext;

        /// <summary>
        /// 原始 MQTT 应用消息。
        /// </summary>
        public MqttApplicationMessage Message => ActionContext.Message;
    }

    /// <summary>
    /// 授权 filter 上下文。
    /// </summary>
    public sealed class MqttAuthorizationFilterContext : MqttFilterContext
    {
        internal MqttAuthorizationFilterContext(
            MqttActionContext actionContext,
            IReadOnlyList<IMqttFilterMetadata> filters)
            : base(actionContext, filters)
        {
        }

        /// <summary>
        /// 授权失败或短路时要执行的 MQTT result。
        /// </summary>
        public MqttResult? Result { get; set; }
    }

    /// <summary>
    /// resource filter 执行前上下文。
    /// </summary>
    public sealed class MqttResourceExecutingContext : MqttFilterContext
    {
        internal MqttResourceExecutingContext(
            MqttActionContext actionContext,
            IReadOnlyList<IMqttFilterMetadata> filters,
            object controller,
            MethodInfo actionMethod)
            : base(actionContext, filters)
        {
            Controller = controller ?? throw new ArgumentNullException(nameof(controller));
            ActionMethod = actionMethod ?? throw new ArgumentNullException(nameof(actionMethod));
        }

        /// <summary>
        /// 当前 controller 实例。
        /// </summary>
        public object Controller { get; }

        /// <summary>
        /// 当前 action 方法。
        /// </summary>
        public MethodInfo ActionMethod { get; }

        /// <summary>
        /// 短路后续管线时要执行的 MQTT result。
        /// </summary>
        public MqttResult? Result { get; set; }
    }

    /// <summary>
    /// resource filter 执行后上下文。
    /// </summary>
    public sealed class MqttResourceExecutedContext : MqttFilterContext
    {
        /// <summary>
        /// 创建 resource filter 执行后上下文。
        /// </summary>
        /// <param name="actionContext">当前 MQTT action 上下文。</param>
        /// <param name="filters">当前 action 关联的全部 filter 实例。</param>
        /// <param name="controller">当前 controller 实例。</param>
        /// <param name="actionMethod">当前 action 方法。</param>
        /// <param name="result">资源管线产生的 result。</param>
        /// <param name="exception">资源或 action 管线抛出的异常。</param>
        /// <param name="canceled">是否由 filter 短路。</param>
        public MqttResourceExecutedContext(
            MqttActionContext actionContext,
            IReadOnlyList<IMqttFilterMetadata> filters,
            object controller,
            MethodInfo actionMethod,
            MqttResult? result,
            Exception? exception,
            bool canceled)
            : base(actionContext, filters)
        {
            Controller = controller ?? throw new ArgumentNullException(nameof(controller));
            ActionMethod = actionMethod ?? throw new ArgumentNullException(nameof(actionMethod));
            Result = result;
            Exception = exception;
            Canceled = canceled;
        }

        /// <summary>
        /// 当前 controller 实例。
        /// </summary>
        public object Controller { get; }

        /// <summary>
        /// 当前 action 方法。
        /// </summary>
        public MethodInfo ActionMethod { get; }

        /// <summary>
        /// 资源管线产生的 result。
        /// </summary>
        public MqttResult? Result { get; set; }

        /// <summary>
        /// 资源或 action 管线抛出的异常。
        /// </summary>
        public Exception? Exception { get; set; }

        /// <summary>
        /// 标记异常是否已被 filter 处理。
        /// </summary>
        public bool ExceptionHandled { get; set; }

        /// <summary>
        /// 是否由 filter 短路。
        /// </summary>
        public bool Canceled { get; }
    }

    /// <summary>
    /// action filter 执行前上下文。
    /// </summary>
    public sealed class MqttActionExecutingContext : MqttFilterContext
    {
        internal MqttActionExecutingContext(
            MqttActionContext actionContext,
            IReadOnlyList<IMqttFilterMetadata> filters,
            object controller,
            MethodInfo actionMethod,
            object?[]? actionArguments)
            : base(actionContext, filters)
        {
            Controller = controller ?? throw new ArgumentNullException(nameof(controller));
            ActionMethod = actionMethod ?? throw new ArgumentNullException(nameof(actionMethod));
            ActionArguments = actionArguments ?? Array.Empty<object?>();
        }

        /// <summary>
        /// 当前 controller 实例。
        /// </summary>
        public object Controller { get; }

        /// <summary>
        /// 当前 action 方法。
        /// </summary>
        public MethodInfo ActionMethod { get; }

        /// <summary>
        /// 已绑定的 action 参数。
        /// </summary>
        public IReadOnlyList<object?> ActionArguments { get; }

        /// <summary>
        /// 短路 action 时要执行的 MQTT result。
        /// </summary>
        public MqttResult? Result { get; set; }
    }

    /// <summary>
    /// action filter 执行后上下文。
    /// </summary>
    public sealed class MqttActionExecutedContext : MqttFilterContext
    {
        /// <summary>
        /// 创建 action filter 执行后上下文。
        /// </summary>
        /// <param name="actionContext">当前 MQTT action 上下文。</param>
        /// <param name="filters">当前 action 关联的全部 filter 实例。</param>
        /// <param name="controller">当前 controller 实例。</param>
        /// <param name="actionMethod">当前 action 方法。</param>
        /// <param name="actionArguments">已绑定的 action 参数。</param>
        /// <param name="result">action 产生的 MQTT result。</param>
        /// <param name="exception">action 抛出的异常。</param>
        /// <param name="canceled">是否由 filter 短路。</param>
        public MqttActionExecutedContext(
            MqttActionContext actionContext,
            IReadOnlyList<IMqttFilterMetadata> filters,
            object controller,
            MethodInfo actionMethod,
            object?[]? actionArguments,
            MqttResult? result,
            Exception? exception,
            bool canceled)
            : base(actionContext, filters)
        {
            Controller = controller ?? throw new ArgumentNullException(nameof(controller));
            ActionMethod = actionMethod ?? throw new ArgumentNullException(nameof(actionMethod));
            ActionArguments = actionArguments ?? Array.Empty<object?>();
            Result = result;
            Exception = exception;
            Canceled = canceled;
        }

        /// <summary>
        /// 当前 controller 实例。
        /// </summary>
        public object Controller { get; }

        /// <summary>
        /// 当前 action 方法。
        /// </summary>
        public MethodInfo ActionMethod { get; }

        /// <summary>
        /// 已绑定的 action 参数。
        /// </summary>
        public IReadOnlyList<object?> ActionArguments { get; }

        /// <summary>
        /// action 产生的 MQTT result。
        /// </summary>
        public MqttResult? Result { get; set; }

        /// <summary>
        /// action 抛出的异常。
        /// </summary>
        public Exception? Exception { get; set; }

        /// <summary>
        /// 标记异常是否已被 filter 处理。
        /// </summary>
        public bool ExceptionHandled { get; set; }

        /// <summary>
        /// 是否由 filter 短路。
        /// </summary>
        public bool Canceled { get; }
    }

    /// <summary>
    /// exception filter 上下文。
    /// </summary>
    public sealed class MqttExceptionContext : MqttFilterContext
    {
        internal MqttExceptionContext(
            MqttActionContext actionContext,
            IReadOnlyList<IMqttFilterMetadata> filters,
            Exception exception)
            : base(actionContext, filters)
        {
            Exception = exception ?? throw new ArgumentNullException(nameof(exception));
        }

        /// <summary>
        /// 管线捕获到的异常。
        /// </summary>
        public Exception Exception { get; }

        /// <summary>
        /// 标记异常是否已被处理。
        /// </summary>
        public bool ExceptionHandled { get; set; }

        /// <summary>
        /// 异常被处理后要执行的 MQTT result。
        /// </summary>
        public MqttResult? Result { get; set; }
    }

    /// <summary>
    /// result filter 执行前上下文。
    /// </summary>
    public sealed class MqttResultExecutingContext : MqttFilterContext
    {
        internal MqttResultExecutingContext(
            MqttActionContext actionContext,
            IReadOnlyList<IMqttFilterMetadata> filters,
            MqttResult result)
            : base(actionContext, filters)
        {
            Result = result ?? throw new ArgumentNullException(nameof(result));
        }

        /// <summary>
        /// 将要执行的 MQTT result；filter 可以替换它。
        /// </summary>
        public MqttResult Result { get; set; }

        /// <summary>
        /// 是否取消 result 执行。
        /// </summary>
        public bool Cancel { get; set; }
    }

    /// <summary>
    /// result filter 执行后上下文。
    /// </summary>
    public sealed class MqttResultExecutedContext : MqttFilterContext
    {
        /// <summary>
        /// 创建 result filter 执行后上下文。
        /// </summary>
        /// <param name="actionContext">当前 MQTT action 上下文。</param>
        /// <param name="filters">当前 action 关联的全部 filter 实例。</param>
        /// <param name="result">已执行或被短路的 MQTT result。</param>
        /// <param name="exception">result 执行抛出的异常。</param>
        /// <param name="canceled">是否由 filter 取消。</param>
        public MqttResultExecutedContext(
            MqttActionContext actionContext,
            IReadOnlyList<IMqttFilterMetadata> filters,
            MqttResult result,
            Exception? exception,
            bool canceled)
            : base(actionContext, filters)
        {
            Result = result ?? throw new ArgumentNullException(nameof(result));
            Exception = exception;
            Canceled = canceled;
        }

        /// <summary>
        /// 已执行或被短路的 MQTT result。
        /// </summary>
        public MqttResult Result { get; }

        /// <summary>
        /// result 执行抛出的异常。
        /// </summary>
        public Exception? Exception { get; set; }

        /// <summary>
        /// 标记异常是否已被 filter 处理。
        /// </summary>
        public bool ExceptionHandled { get; set; }

        /// <summary>
        /// 是否由 filter 取消。
        /// </summary>
        public bool Canceled { get; }
    }
}
