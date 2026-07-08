// Copyright (c) Atlas Lift Tech Inc. All rights reserved.

namespace MQTTnet.AspNetCore.Routing
{
#nullable enable

    public sealed class MqttApplicationMessageDispatchResult
    {
        /// <summary>
        /// 创建消息分发结果。
        /// </summary>
        /// <param name="isHandled">消息是否被成功处理。</param>
        /// <param name="modelState">绑定错误集合。</param>
        public MqttApplicationMessageDispatchResult(
            bool isHandled,
            MqttModelStateDictionary? modelState = null)
        {
            IsHandled = isHandled;
            ModelState = modelState ?? new MqttModelStateDictionary();
        }

        /// <summary>
        /// 消息是否被成功处理。
        /// </summary>
        public bool IsHandled { get; }

        /// <summary>
        /// 消息分发过程中的绑定错误集合。
        /// </summary>
        public MqttModelStateDictionary ModelState { get; }
    }
}
