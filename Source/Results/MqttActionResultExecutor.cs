using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading.Tasks;

#nullable enable

namespace MQTTnet.AspNetCore.Routing
{
    /// <summary>
    /// 执行 MQTT action 返回值，将 CLR 返回值映射为 MQTT result。
    /// </summary>
    internal sealed class MqttActionResultExecutor
    {
        /// <summary>
        /// 执行 action 返回值。
        /// </summary>
        /// <param name="declaredReturnType">action 声明返回类型。</param>
        /// <param name="returnValue">action 实际返回值。</param>
        /// <param name="actionContext">当前 MQTT action 上下文。</param>
        public async ValueTask ExecuteAsync(
            Type declaredReturnType,
            object? returnValue,
            MqttActionContext actionContext)
        {
            var result = await CreateResultAsync(declaredReturnType, returnValue, actionContext)
                .ConfigureAwait(false);
            await ExecuteResultAsync(result, actionContext).ConfigureAwait(false);
        }

        /// <summary>
        /// 将 action 返回值转换为 MQTT result，但不立即执行 result。
        /// </summary>
        /// <param name="declaredReturnType">action 声明返回类型。</param>
        /// <param name="returnValue">action 实际返回值。</param>
        /// <param name="actionContext">当前 MQTT action 上下文。</param>
        public async ValueTask<MqttResult> CreateResultAsync(
            Type declaredReturnType,
            object? returnValue,
            MqttActionContext actionContext)
        {
            if (declaredReturnType == null)
            {
                throw new ArgumentNullException(nameof(declaredReturnType));
            }

            if (actionContext == null)
            {
                throw new ArgumentNullException(nameof(actionContext));
            }

            if (declaredReturnType == typeof(void))
            {
                return MqttEmptyResult.Instance;
            }

            var resultType = declaredReturnType;
            var resultValue = returnValue;

            if (declaredReturnType == typeof(Task))
            {
                await AwaitTask((Task?)returnValue, declaredReturnType).ConfigureAwait(false);
                return MqttEmptyResult.Instance;
            }

            if (declaredReturnType == typeof(ValueTask))
            {
                await AwaitValueTask(returnValue, declaredReturnType).ConfigureAwait(false);
                return MqttEmptyResult.Instance;
            }

            if (TryGetTaskResultType(declaredReturnType, out var taskResultType))
            {
                var task = await AwaitTask((Task?)returnValue, declaredReturnType).ConfigureAwait(false);
                resultValue = GetTaskResult(task, declaredReturnType);
                resultType = taskResultType;
            }
            else if (TryGetValueTaskResultType(declaredReturnType, out var valueTaskResultType))
            {
                var task = await AwaitValueTaskAsTask(returnValue, declaredReturnType).ConfigureAwait(false);
                resultValue = GetTaskResult(task, task.GetType());
                resultType = valueTaskResultType;
            }

            if (resultValue == null && typeof(MqttResult).IsAssignableFrom(resultType))
            {
                throw new NullReferenceException($"MQTT action returned null instead of {resultType.FullName}.");
            }

            if (resultValue is MqttResult mqttResult)
            {
                return mqttResult;
            }

            return new MqttPayloadResult(resultValue, resultType);
        }

        /// <summary>
        /// 执行 MQTT result。
        /// </summary>
        /// <param name="result">要执行的 result。</param>
        /// <param name="actionContext">当前 MQTT action 上下文。</param>
        public ValueTask ExecuteResultAsync(MqttResult result, MqttActionContext actionContext)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            if (actionContext == null)
            {
                throw new ArgumentNullException(nameof(actionContext));
            }

            return result.ExecuteAsync(actionContext);
        }

        private static async ValueTask<Task> AwaitTask(Task? task, Type declaredReturnType)
        {
            if (task == null)
            {
                throw new NullReferenceException($"MQTT action returned null instead of {declaredReturnType.FullName}.");
            }

            await task.ConfigureAwait(false);
            return task;
        }

        private static ValueTask AwaitValueTask(object? valueTask, Type declaredReturnType)
        {
            if (valueTask == null)
            {
                throw new NullReferenceException($"MQTT action returned null instead of {declaredReturnType.FullName}.");
            }

            return (ValueTask)valueTask;
        }

        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2070",
            Justification = "R4 的 controller 路径仍基于 action MethodInfo 调用；这里只读取 ValueTask<T>.AsTask，且不使用 MakeGenericMethod。R6 会用注册期委托或 source generator 替换该反射拆包。")]
        private static async ValueTask<Task> AwaitValueTaskAsTask(object? valueTask, Type declaredReturnType)
        {
            if (valueTask == null)
            {
                throw new NullReferenceException($"MQTT action returned null instead of {declaredReturnType.FullName}.");
            }

            var asTaskMethod = declaredReturnType.GetMethod(nameof(ValueTask.AsTask), BindingFlags.Instance | BindingFlags.Public)
                ?? throw new InvalidOperationException($"Return type '{declaredReturnType.FullName}' does not expose AsTask().");
            var task = (Task?)asTaskMethod.Invoke(valueTask, null);
            return await AwaitTask(task, declaredReturnType).ConfigureAwait(false);
        }

        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2070",
            Justification = "R4 需要支持 Task<T> 返回值；这里只读取 Task<T>.Result，且不使用 MakeGenericMethod。R6 会用注册期委托或 source generator 替换该反射拆包。")]
        private static object? GetTaskResult(Task task, Type taskType)
        {
            var resultProperty = taskType.GetProperty("Result", BindingFlags.Instance | BindingFlags.Public);
            if (resultProperty == null)
            {
                throw new InvalidOperationException($"Task return type '{taskType.FullName}' does not expose Result.");
            }

            return resultProperty.GetValue(task);
        }

        private static bool TryGetTaskResultType(Type returnType, out Type resultType)
        {
            if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                resultType = returnType.GetGenericArguments()[0];
                return true;
            }

            resultType = typeof(void);
            return false;
        }

        private static bool TryGetValueTaskResultType(Type returnType, out Type resultType)
        {
            if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(ValueTask<>))
            {
                resultType = returnType.GetGenericArguments()[0];
                return true;
            }

            resultType = typeof(void);
            return false;
        }
    }
}
