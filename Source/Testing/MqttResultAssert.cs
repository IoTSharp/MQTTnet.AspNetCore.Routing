using MQTTnet.Protocol;
using System;

#nullable enable

namespace MQTTnet.AspNetCore.Routing.Testing
{
    /// <summary>
    /// 与具体测试框架无关的 MQTT result 断言工具。
    /// </summary>
    public static class MqttResultAssert
    {
        /// <summary>
        /// 确认 result 为 acknowledge。
        /// </summary>
        /// <param name="result">待断言 result。</param>
        public static MqttAcknowledgeResult IsAcknowledge(MqttResult result)
        {
            return IsResult<MqttAcknowledgeResult>(result, "acknowledge");
        }

        /// <summary>
        /// 确认 result 为 suppress。
        /// </summary>
        /// <param name="result">待断言 result。</param>
        public static MqttSuppressResult IsSuppress(MqttResult result)
        {
            return IsResult<MqttSuppressResult>(result, "suppress");
        }

        /// <summary>
        /// 确认 result 为 reject。
        /// </summary>
        /// <param name="result">待断言 result。</param>
        /// <param name="reasonCode">期望 reason code；为空时不检查。</param>
        public static MqttRejectResult IsReject(
            MqttResult result,
            MqttPubAckReasonCode? reasonCode = null)
        {
            var reject = IsResult<MqttRejectResult>(result, "reject");
            if (reasonCode.HasValue && reject.ReasonCode != reasonCode.Value)
            {
                throw new MqttRouteTestException(
                    $"Expected reject reason code '{reasonCode.Value}', but found '{reject.ReasonCode}'.");
            }

            return reject;
        }

        /// <summary>
        /// 确认 result 为 publish。
        /// </summary>
        /// <param name="result">待断言 result。</param>
        public static MqttPublishResult IsPublish(MqttResult result)
        {
            return IsResult<MqttPublishResult>(result, "publish");
        }

        /// <summary>
        /// 确认 result 为 payload result。
        /// </summary>
        /// <param name="result">待断言 result。</param>
        public static MqttPayloadResult IsPayload(MqttResult result)
        {
            return IsResult<MqttPayloadResult>(result, "payload");
        }

        /// <summary>
        /// 确认 result 的入站 PUBLISH 处置方式。
        /// </summary>
        /// <param name="result">待断言 result。</param>
        /// <param name="disposition">期望处置方式。</param>
        public static void HasDisposition(
            MqttResult result,
            MqttInboundPublishDisposition? disposition)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            if (result.Disposition != disposition)
            {
                throw new MqttRouteTestException(
                    $"Expected result disposition '{disposition}', but found '{result.Disposition}'.");
            }
        }

        private static TResult IsResult<TResult>(MqttResult result, string name)
            where TResult : MqttResult
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            if (result is TResult typed)
            {
                return typed;
            }

            throw new MqttRouteTestException(
                $"Expected MQTT {name} result, but found '{result.GetType().FullName}'.");
        }
    }
}
