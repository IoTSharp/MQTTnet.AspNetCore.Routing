// Copyright (c) Atlas Lift Tech Inc. All rights reserved.

using MQTTnet.Server;

namespace MQTTnet.AspNetCore.Routing
{
    public class MqttControllerContext : IMqttControllerContext
    {
        public InterceptingPublishEventArgs MqttContext { get; set; }
        public MqttServer MqttServer { get; set; }
        /// <summary>
        /// 当前 controller action 绑定过程产生的错误。
        /// </summary>
        public MqttModelStateDictionary ModelState { get; } = new MqttModelStateDictionary();
        /// <summary>
        /// 当前 controller action 的 R3 执行上下文。
        /// </summary>
        public MqttActionContext ActionContext { get; internal set; }
    }
}
