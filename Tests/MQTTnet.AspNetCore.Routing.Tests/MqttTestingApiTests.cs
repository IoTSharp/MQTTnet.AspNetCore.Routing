using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MQTTnet.AspNetCore.Routing.Attributes;
using MQTTnet.AspNetCore.Routing.Testing;
using MQTTnet.Protocol;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace MQTTnet.AspNetCore.Routing.Tests
{
    [TestClass]
    public sealed partial class MqttTestingApiTests
    {
        [TestMethod]
        public async Task TestHost_ControllerRouting_CanMatchInvokeAndAssertCatalog()
        {
            var recorder = new TestingRecorder();
            using var host = MqttRoutingTestHost.Create(services =>
            {
                services.AddSingleton(recorder);
                services.AddMqttControllers<TestingController>(options =>
                {
                    options.WithJsonSerializerContext(TestingJsonContext.Default);
                });
            });

            var catalogRoute = MqttRouteCatalogAssert.ContainsControllerAction(
                host.Catalog,
                typeof(TestingController),
                nameof(TestingController.Telemetry),
                "testing/devices/{deviceId}/telemetry");
            var match = host.Match("testing/devices/device-17/telemetry");
            var result = await host.InvokeControllerAsync(
                MqttTestMessages.CreateJson(
                    "testing/devices/device-17/telemetry",
                    new TestingPayload { Value = 17 },
                    TestingJsonContext.Default.TestingPayload),
                clientId: "testing-client");

            MqttRouteCatalogAssert.HasNoErrors(host.Catalog);
            Assert.AreEqual("testing/devices/{deviceId}/telemetry", catalogRoute.Template);
            match.EnsureMatched();
            Assert.AreEqual("device-17", match.GetRouteValue<string>("deviceId"));
            Assert.IsTrue(result.IsMatched);
            Assert.IsFalse(result.ProcessPublish);
            Assert.IsTrue(result.ModelState.IsValid);
            Assert.AreEqual(MqttPubAckReasonCode.Success, result.ReasonCode);
            Assert.AreEqual("device-17|17|testing-client", await recorder.ExpectAsync());
        }

        [TestMethod]
        public async Task TestHost_SlimRouting_CanMatchAndDispatch()
        {
            var recorder = new TestingRecorder();
            using var host = MqttRoutingTestHost.Create(services =>
            {
                services.AddMqttApplicationMessageSlimRouting(routes =>
                {
                    routes.MapJson(
                        "testing/slim/{deviceId}/telemetry",
                        TestingJsonContext.Default.TestingPayload,
                        (context, payload) =>
                        {
                            recorder.Record(
                                $"{context.GetRouteValue("deviceId")}|{payload.Value}|{context.ClientId}");
                            return ValueTask.CompletedTask;
                        });
                });
            });

            var match = host.Match("testing/slim/device-42/telemetry");
            var result = await host.DispatchApplicationMessageAsync(
                MqttTestMessages.CreateJson(
                    "testing/slim/device-42/telemetry",
                    new TestingPayload { Value = 42 },
                    TestingJsonContext.Default.TestingPayload),
                "slim-client");

            match.EnsureMatched();
            Assert.AreEqual("device-42", match.GetRouteValue<string>("deviceId"));
            Assert.IsTrue(result.IsHandled);
            Assert.IsTrue(result.ModelState.IsValid);
            Assert.AreEqual("device-42|42|slim-client", await recorder.ExpectAsync());
        }

        [TestMethod]
        public async Task TestResults_CanConvertAndExecuteResults()
        {
            var reject = await MqttTestResults.CreateResultAsync(
                typeof(Task<MqttResult>),
                Task.FromResult<MqttResult>(new MqttRejectResult(MqttPubAckReasonCode.NotAuthorized)));
            var rejectResult = MqttResultAssert.IsReject(reject, MqttPubAckReasonCode.NotAuthorized);
            MqttResultAssert.HasDisposition(rejectResult, MqttInboundPublishDisposition.Reject);

            var message = MqttTestMessages.Create("testing/results", "payload");
            var interceptingPublish = MqttTestContexts.CreateInterceptingPublishContext(message);
            var actionContext = MqttTestContexts.CreateActionContext(
                message,
                interceptingPublishContext: interceptingPublish);

            await MqttTestResults.ExecuteAsync(new MqttSuppressResult("handled"), actionContext);

            Assert.IsFalse(interceptingPublish.ProcessPublish);
            Assert.AreEqual(MqttPubAckReasonCode.Success, interceptingPublish.Response.ReasonCode);
            Assert.AreEqual("handled", interceptingPublish.Response.ReasonString);
        }

        [MqttController]
        [MqttRoute("testing/devices")]
        public sealed class TestingController : MqttBaseController
        {
            private readonly TestingRecorder _recorder;

            public TestingController(TestingRecorder recorder)
            {
                _recorder = recorder;
            }

            [MqttRoute("{deviceId}/telemetry")]
            public MqttResult Telemetry(
                [FromMqttRoute] string deviceId,
                [FromMqttPayload] TestingPayload payload,
                [FromMqttClient] string clientId)
            {
                _recorder.Record($"{deviceId}|{payload.Value}|{clientId}");
                return Suppress();
            }
        }

        public sealed class TestingPayload
        {
            public int Value { get; set; }
        }

        [JsonSerializable(typeof(TestingPayload))]
        public sealed partial class TestingJsonContext : JsonSerializerContext
        {
        }

        public sealed class TestingRecorder
        {
            private readonly TaskCompletionSource<string> _source =
                new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

            public void Record(string value)
            {
                _source.TrySetResult(value);
            }

            public async Task<string> ExpectAsync()
            {
                var completed = await Task.WhenAny(
                    _source.Task,
                    Task.Delay(5000)).ConfigureAwait(false);
                if (completed != _source.Task)
                {
                    Assert.Fail("Timed out waiting for test route execution.");
                }

                return await _source.Task.ConfigureAwait(false);
            }
        }
    }
}
