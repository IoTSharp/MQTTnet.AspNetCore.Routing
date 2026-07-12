using System;
using System.Threading.Tasks;

#nullable enable

namespace MQTTnet.AspNetCore.Routing
{
    /// <summary>
    /// source generator 产物使用的 controller 初始化与 result 执行辅助方法。
    /// </summary>
    public static class MqttGeneratedEndpointHelpers
    {
        /// <summary>
        /// 把 server publish 上下文注入生成式 controller。
        /// </summary>
        /// <param name="controller">生成代码直接构造的 controller。</param>
        /// <param name="context">当前生成式 route 上下文。</param>
        public static void InitializeController(
            MqttBaseController controller,
            MqttApplicationMessageRouteContext context)
        {
            if (controller == null)
            {
                throw new ArgumentNullException(nameof(controller));
            }

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var publishContext = context.InterceptingPublishContext
                ?? throw new InvalidOperationException("生成式 MQTT controller 只能在 server publish 路径执行。");
            var mqttServer = context.MqttServer
                ?? throw new InvalidOperationException("生成式 MQTT controller 缺少 MQTT server 实例。");

            controller.ControllerContext = new MqttControllerContext
            {
                MqttContext = publishContext,
                MqttServer = mqttServer,
                ActionContext = context.ActionContext,
            };
        }

        /// <summary>
        /// 执行生成式 action 返回的 MQTT result。
        /// </summary>
        /// <param name="result">action 返回结果。</param>
        /// <param name="context">当前生成式 route 上下文。</param>
        public static ValueTask ExecuteResultAsync(
            MqttResult result,
            MqttApplicationMessageRouteContext context)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            return result.ExecuteAsync(context.ActionContext);
        }
    }
}
