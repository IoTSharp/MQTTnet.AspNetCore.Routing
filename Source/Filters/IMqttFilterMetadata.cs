using System.Threading.Tasks;

#nullable enable

namespace MQTTnet.AspNetCore.Routing
{
    /// <summary>
    /// MQTT filter 的通用元数据标记接口。
    /// </summary>
    public interface IMqttFilterMetadata
    {
    }

    /// <summary>
    /// 表示带排序值的 MQTT filter。
    /// </summary>
    public interface IOrderedMqttFilter : IMqttFilterMetadata
    {
        /// <summary>
        /// filter 排序值；数值越小越先进入管线。
        /// </summary>
        int Order { get; }
    }

    /// <summary>
    /// 在资源绑定和 action 执行前执行授权检查。
    /// </summary>
    public interface IMqttAuthorizationFilter : IMqttFilterMetadata
    {
        /// <summary>
        /// 执行授权检查；设置 <see cref="MqttAuthorizationFilterContext.Result"/> 可短路后续管线。
        /// </summary>
        /// <param name="context">授权 filter 上下文。</param>
        ValueTask OnAuthorizationAsync(MqttAuthorizationFilterContext context);
    }

    /// <summary>
    /// 包裹 MQTT action 调用的资源 filter。
    /// </summary>
    public interface IMqttResourceFilter : IMqttFilterMetadata
    {
        /// <summary>
        /// 执行资源 filter，可选择调用 <paramref name="next"/> 继续管线。
        /// </summary>
        /// <param name="context">资源 filter 执行前上下文。</param>
        /// <param name="next">后续资源/action 管线。</param>
        ValueTask<MqttResourceExecutedContext> OnResourceExecutionAsync(
            MqttResourceExecutingContext context,
            MqttResourceExecutionDelegate next);
    }

    /// <summary>
    /// 包裹 MQTT action 方法的 action filter。
    /// </summary>
    public interface IMqttActionFilter : IMqttFilterMetadata
    {
        /// <summary>
        /// 执行 action filter，可选择调用 <paramref name="next"/> 执行 action。
        /// </summary>
        /// <param name="context">action filter 执行前上下文。</param>
        /// <param name="next">后续 action 管线。</param>
        ValueTask<MqttActionExecutedContext> OnActionExecutionAsync(
            MqttActionExecutingContext context,
            MqttActionExecutionDelegate next);
    }

    /// <summary>
    /// 将 MQTT action 或 binding 过程中的异常转换为 MQTT result。
    /// </summary>
    public interface IMqttExceptionFilter : IMqttFilterMetadata
    {
        /// <summary>
        /// 执行异常 filter；设置 <see cref="MqttExceptionContext.ExceptionHandled"/> 和
        /// <see cref="MqttExceptionContext.Result"/> 可恢复管线。
        /// </summary>
        /// <param name="context">异常上下文。</param>
        ValueTask OnExceptionAsync(MqttExceptionContext context);
    }

    /// <summary>
    /// 包裹 MQTT result 执行的 result filter。
    /// </summary>
    public interface IMqttResultFilter : IMqttFilterMetadata
    {
        /// <summary>
        /// 执行 result filter，可选择调用 <paramref name="next"/> 写入最终 MQTT 处置。
        /// </summary>
        /// <param name="context">result filter 执行前上下文。</param>
        /// <param name="next">后续 result 管线。</param>
        ValueTask<MqttResultExecutedContext> OnResultExecutionAsync(
            MqttResultExecutingContext context,
            MqttResultExecutionDelegate next);
    }

    /// <summary>
    /// 资源 filter 的后续管线委托。
    /// </summary>
    public delegate ValueTask<MqttResourceExecutedContext> MqttResourceExecutionDelegate();

    /// <summary>
    /// action filter 的后续管线委托。
    /// </summary>
    public delegate ValueTask<MqttActionExecutedContext> MqttActionExecutionDelegate();

    /// <summary>
    /// result filter 的后续管线委托。
    /// </summary>
    public delegate ValueTask<MqttResultExecutedContext> MqttResultExecutionDelegate();
}
