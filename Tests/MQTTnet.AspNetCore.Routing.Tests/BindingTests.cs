using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MQTTnet;
using MQTTnet.AspNetCore.Routing.Attributes;
using MQTTnet.Protocol;
using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

#nullable enable

namespace MQTTnet.AspNetCore.Routing.Tests
{
    [TestClass]
    public sealed class BindingTests
    {
        [TestMethod]
        public async Task Binder_BindsSessionAndUserProperties()
        {
            var actionContext = CreateActionContext(
                MqttMessage(
                    "binding/devices/a",
                    "ignored",
                    ("trace-id", "trace-777"),
                    ("tag", "a"),
                    ("tag", "b")),
                sessionItems: new Hashtable
                {
                    ["tenant"] = "tenant-a",
                    ["retry"] = "3"
                });
            var binder = CreateBinder();

            var values = await binder.BindAsync(
                Parameters(nameof(BindingTarget.SessionAndUserProperties)),
                actionContext);

            Assert.AreEqual("tenant-a", values[0]);
            Assert.AreEqual(3, values[1]);
            Assert.AreEqual("trace-777", values[2]);
            CollectionAssert.AreEqual(new[] { "a", "b" }, (string[])values[3]!);
        }

        [TestMethod]
        public async Task Binder_BindsRawPayloadAndContext()
        {
            var actionContext = CreateActionContext(MqttMessage("binding/raw", "abc"));
            var binder = CreateBinder();

            var values = await binder.BindAsync(
                Parameters(nameof(BindingTarget.RawPayloadAndContext)),
                actionContext);

            var payload = (ReadOnlySequence<byte>)values[0]!;
            var text = Encoding.UTF8.GetString(payload.ToArray());

            Assert.AreEqual("abc", text);
            Assert.AreSame(actionContext, values[1]);
            Assert.AreSame(actionContext.RequestContext, values[2]);
            Assert.AreSame(actionContext.RouteContext, values[3]);
        }

        [TestMethod]
        public async Task Binder_ReturnsModelStateForMissingSessionItem()
        {
            var actionContext = CreateActionContext(MqttMessage("binding/missing", "ignored"));
            var binder = CreateBinder();

            var exception = await AssertThrowsAsync<MqttBindingException>(() =>
                binder.BindAsync(Parameters(nameof(BindingTarget.MissingSessionItem)), actionContext).AsTask());

            Assert.AreSame(actionContext.ModelState, exception.ModelState);
            Assert.IsTrue(actionContext.ModelState.TryGetErrors("tenant", out var errors));
            Assert.IsTrue(errors.Any(error => error.ErrorCode == MqttBindingErrorCode.MissingSessionItem));
        }

        private static MqttActionParameterBinder CreateBinder()
        {
            var options = new MqttRoutingOptions()
                .WithJsonSerializerOptions(new JsonSerializerOptions(JsonSerializerDefaults.Web));
            options.InputFormatters.Add(new MqttBinaryPayloadInputFormatter());
            options.InputFormatters.Add(new MqttJsonPayloadInputFormatter());
            return new MqttActionParameterBinder(options);
        }

        private static MqttActionContext CreateActionContext(
            MqttApplicationMessage message,
            IDictionary? sessionItems = null)
        {
            var services = new ServiceCollection().BuildServiceProvider();
            var requestContext = new MqttRequestContext(
                message,
                "binding-client",
                sessionItems);
            var routeContext = new MqttRouteContext(
                null,
                new Dictionary<string, object?>
                {
                    ["deviceId"] = "binding-device"
                });
            return new MqttActionContext(
                requestContext,
                routeContext,
                new MqttModelStateDictionary(),
                services);
        }

        private static ParameterInfo[] Parameters(string methodName)
        {
            return (typeof(BindingTarget)
                    .GetMethod(methodName, BindingFlags.Public | BindingFlags.Static)
                ?? throw new InvalidOperationException($"Method '{methodName}' was not found."))
                .GetParameters();
        }

        private static MqttApplicationMessage MqttMessage(
            string topic,
            string payload,
            params (string Name, string Value)[] userProperties)
        {
            var builder = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce);

            foreach (var userProperty in userProperties)
            {
                builder.WithUserProperty(
                    userProperty.Name,
                    Encoding.UTF8.GetBytes(userProperty.Value));
            }

            return builder.Build();
        }

        private static async Task<TException> AssertThrowsAsync<TException>(Func<Task> action)
            where TException : Exception
        {
            try
            {
                await action().ConfigureAwait(false);
            }
            catch (TException exception)
            {
                return exception;
            }
            catch (Exception exception)
            {
                Assert.Fail($"Expected {typeof(TException).Name}, but caught {exception.GetType().Name}: {exception.Message}");
            }

            Assert.Fail($"Expected {typeof(TException).Name}, but no exception was thrown.");
            throw new InvalidOperationException("Unreachable assertion path.");
        }

        public static class BindingTarget
        {
            public static void SessionAndUserProperties(
                [FromMqttSession("tenant")] string tenant,
                [FromMqttSession("retry")] int retry,
                [FromMqttUserProperty("trace-id")] string traceId,
                [FromMqttUserProperty("tag")] string[] tags)
            {
            }

            public static void RawPayloadAndContext(
                [FromMqttPayload(FormatterName = "binary")] ReadOnlySequence<byte> payload,
                MqttActionContext actionContext,
                MqttRequestContext requestContext,
                MqttRouteContext routeContext)
            {
            }

            public static void MissingSessionItem([FromMqttSession("tenant")] string tenant)
            {
            }
        }
    }
}
