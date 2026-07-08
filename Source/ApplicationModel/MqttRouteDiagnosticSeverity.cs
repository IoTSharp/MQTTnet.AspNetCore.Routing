namespace MQTTnet.AspNetCore.Routing
{
    /// <summary>
    /// route catalog 诊断的严重程度。
    /// </summary>
    public enum MqttRouteDiagnosticSeverity
    {
        /// <summary>
        /// 信息级诊断。
        /// </summary>
        Info = 0,

        /// <summary>
        /// 警告级诊断。
        /// </summary>
        Warning = 1,

        /// <summary>
        /// 错误级诊断，通常表示启动期应失败。
        /// </summary>
        Error = 2
    }
}
