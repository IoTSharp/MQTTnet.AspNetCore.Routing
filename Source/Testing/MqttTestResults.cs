using System;
using System.Threading.Tasks;

#nullable enable

namespace MQTTnet.AspNetCore.Routing.Testing
{
    /// <summary>
    /// 用于测试 action 返回值到 MQTT result 映射的辅助工具。
    /// </summary>
    public static class MqttTestResults
    {
        /// <summary>
        /// 将 action 返回值转换为 MQTT result，但不执行 result。
        /// </summary>
        /// <param name="declaredReturnType">action 声明返回类型。</param>
        /// <param name="returnValue">action 实际返回值。</param>
        /// <param name="actionContext">测试 action 上下文；为空时创建默认上下文。</param>
        public static ValueTask<MqttResult> CreateResultAsync(
            Type declaredReturnType,
            object? returnValue,
            MqttActionContext? actionContext = null)
        {
            var context = actionContext ?? MqttTestContexts.CreateActionContext(
                MqttTestMessages.Create("mqtt/test"));
            var executor = new MqttActionResultExecutor();
            return executor.CreateResultAsync(declaredReturnType, returnValue, context);
        }

        /// <summary>
        /// 执行 MQTT result。
        /// </summary>
        /// <param name="result">待执行 result。</param>
        /// <param name="actionContext">测试 action 上下文；为空时创建默认上下文。</param>
        public static ValueTask ExecuteAsync(
            MqttResult result,
            MqttActionContext? actionContext = null)
        {
            var context = actionContext ?? MqttTestContexts.CreateActionContext(
                MqttTestMessages.Create("mqtt/test"));
            var executor = new MqttActionResultExecutor();
            return executor.ExecuteResultAsync(result, context);
        }
    }
}
