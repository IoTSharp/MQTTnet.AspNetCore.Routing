namespace MQTTnet.AspNetCore.Routing
{
    /// <summary>
    /// 描述 action 对当前入站 PUBLISH 的处置方式。
    /// </summary>
    public enum MqttInboundPublishDisposition
    {
        /// <summary>
        /// 继续让 broker 按原 topic 投递当前 PUBLISH。
        /// </summary>
        Continue = 0,

        /// <summary>
        /// action 已消费当前 PUBLISH，不再继续投递原消息，但对发送端返回成功确认。
        /// </summary>
        Suppress = 1,

        /// <summary>
        /// 拒绝当前 PUBLISH，不继续投递原消息，并返回失败 reason code。
        /// </summary>
        Reject = 2
    }
}
