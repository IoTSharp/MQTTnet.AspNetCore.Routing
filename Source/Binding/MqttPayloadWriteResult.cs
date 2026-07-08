using System;

#nullable enable

namespace MQTTnet.AspNetCore.Routing
{
    /// <summary>
    /// payload 输出 formatter 的写入结果。
    /// </summary>
    public sealed class MqttPayloadWriteResult
    {
        /// <summary>
        /// 创建写入结果。
        /// </summary>
        /// <param name="payload">序列化后的 payload。</param>
        /// <param name="contentType">输出内容类型。</param>
        public MqttPayloadWriteResult(ReadOnlyMemory<byte> payload, string? contentType = null)
        {
            Payload = payload;
            ContentType = contentType;
        }

        /// <summary>
        /// 序列化后的 payload。
        /// </summary>
        public ReadOnlyMemory<byte> Payload { get; }

        /// <summary>
        /// 输出内容类型。
        /// </summary>
        public string? ContentType { get; }
    }
}
