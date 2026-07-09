using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MQTTnet;
using MQTTnet.AspNetCore.Routing.Attributes;
using MQTTnet.Packets;
using MQTTnet.Protocol;
using MQTTnet.Server;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace MQTTnet.AspNetCore.Routing.Tests
{
    [TestClass]
    public partial class MqttRoutingIntegrationTests
    {
        [TestMethod]
        public async Task FullRouting_ServerMode_RoutesClientPublishesByTopic()
        {
            var recorder = new FullRoutingRecorder();
            using var services = BuildFullRoutingServices(recorder);
            await using var broker = await MqttTestBroker.StartAsync(services);
            await using var publisher = await MqttTestClient.ConnectAsync(broker.Port, "full-routing-publisher");

            var literalResult = await publisher.PublishStringAsync("full/devices/special/status", "literal");
            var telemetryResult = await publisher.PublishStringAsync("full/devices/42/telemetry", "{\"Temperature\":23.5}");
            var unmatchedResult = await publisher.PublishStringAsync("full/devices/42/unknown", "ignored");

            Assert.IsTrue(literalResult.IsSuccess, literalResult.ReasonString);
            Assert.IsTrue(telemetryResult.IsSuccess, telemetryResult.ReasonString);
            Assert.IsTrue(unmatchedResult.IsSuccess, unmatchedResult.ReasonString);

            var literal = await recorder.ExpectAsync("literal");
            var telemetry = await recorder.ExpectAsync("telemetry");
            var wildcard = await recorder.ExpectAsync("wildcard");

            Assert.AreEqual("special", literal.DeviceId);
            Assert.AreEqual("literal", literal.PayloadText);
            Assert.AreSame(broker.Server, literal.Server);

            Assert.AreEqual("42", telemetry.DeviceId);
            Assert.AreEqual(23.5, telemetry.Temperature);
            Assert.AreEqual("full-routing-publisher", telemetry.ClientId);
            Assert.AreEqual("full/devices/42/telemetry", telemetry.Topic);

            Assert.AreEqual("42/unknown", wildcard.DeviceId);
            Assert.AreEqual("ignored", wildcard.PayloadText);
            Assert.AreEqual("full-routing-publisher", wildcard.ClientId);
        }

        [TestMethod]
        public async Task FullRouting_ServerMode_BindsGuidEnumAndNullableRouteValues()
        {
            var recorder = new FullRoutingRecorder();
            using var services = BuildFullRoutingServices(recorder);
            await using var broker = await MqttTestBroker.StartAsync(services);
            await using var publisher = await MqttTestClient.ConnectAsync(broker.Port, "full-routing-typed-publisher");
            var deviceId = Guid.Parse("4d218938-4175-4a70-99b2-d63896302244");

            var withoutRevision = await publisher.PublishStringAsync($"full/devices/{deviceId:D}/mode/online", "{}");
            var withRevision = await publisher.PublishStringAsync($"full/devices/{deviceId:D}/mode/offline/42", "{}");

            Assert.IsTrue(withoutRevision.IsSuccess, withoutRevision.ReasonString);
            Assert.IsTrue(withRevision.IsSuccess, withRevision.ReasonString);

            var typed = await recorder.ExpectAsync("typed-route");
            var typedWithRevision = await recorder.ExpectAsync("typed-route-revision");

            Assert.AreEqual(deviceId.ToString("D"), typed.DeviceId);
            Assert.AreEqual("Online:<null>", typed.PayloadText);
            Assert.AreEqual(deviceId.ToString("D"), typedWithRevision.DeviceId);
            Assert.AreEqual("Offline:42", typedWithRevision.PayloadText);
        }

        [TestMethod]
        public async Task FullRouting_ServerMode_BindsR3Sources()
        {
            var recorder = new FullRoutingRecorder();
            using var services = BuildFullRoutingServices(recorder);
            await using var broker = await MqttTestBroker.StartAsync(services);
            await using var publisher = await MqttTestClient.ConnectAsync(broker.Port, "full-routing-r3-publisher");

            var publishResult = await publisher.Client.PublishAsync(
                MqttMessage(
                    "full/devices/r3-device/r3",
                    "{\"Temperature\":18.75}",
                    ("trace-id", "trace-42")),
                CancellationToken.None);

            Assert.IsTrue(publishResult.IsSuccess, publishResult.ReasonString);

            var record = await recorder.ExpectAsync("r3-binding");

            Assert.AreEqual("r3-device", record.DeviceId);
            Assert.AreEqual(18.75, record.Temperature);
            Assert.AreEqual("full-routing-r3-publisher", record.ClientId);
            Assert.AreEqual("full/devices/r3-device/r3", record.Topic);
            Assert.AreEqual("full-routing-r3-publisher|trace-42|full/devices/r3-device/r3|True|True", record.PayloadText);
        }

        [TestMethod]
        public async Task FullRouting_ServerMode_ReturnsPayloadToResponseTopic()
        {
            var recorder = new FullRoutingRecorder();
            using var services = BuildFullRoutingServices(recorder);
            await using var broker = await MqttTestBroker.StartAsync(services);
            await using var subscriber = await MqttTestClient.ConnectAsync(broker.Port, "full-routing-rpc-subscriber");
            await using var publisher = await MqttTestClient.ConnectAsync(broker.Port, "full-routing-rpc-publisher");
            var responseTopic = "full/replies/rpc-1";
            var correlationData = new byte[] { 1, 2, 3, 4 };
            var responseTask = CaptureNextMessageAsync(subscriber);

            var subscription = await subscriber.SubscribeAsync(responseTopic);
            Assert.AreEqual(MqttClientSubscribeResultCode.GrantedQoS0, subscription.Items.Single().ResultCode);

            var publishResult = await publisher.Client.PublishAsync(
                new MqttApplicationMessageBuilder()
                    .WithTopic("full/devices/return/rpc")
                    .WithPayload("{\"Temperature\":31.25}")
                    .WithResponseTopic(responseTopic)
                    .WithCorrelationData(correlationData)
                    .Build(),
                CancellationToken.None);

            Assert.IsTrue(publishResult.IsSuccess, publishResult.ReasonString);

            var response = await ExpectMessageAsync(responseTask);
            var responsePayload = JsonSerializer.Deserialize(
                response.ConvertPayloadToString(),
                TestJsonContext.Default.FullResponsePayload);

            Assert.AreEqual(responseTopic, response.Topic);
            CollectionAssert.AreEqual(correlationData, response.CorrelationData);
            Assert.IsNotNull(responsePayload);
            Assert.AreEqual("rpc", responsePayload.Kind);
            Assert.AreEqual(31.25, responsePayload.Echo);
            Assert.AreEqual("full-routing-rpc-publisher", responsePayload.ClientId);

            var record = await recorder.ExpectAsync("rpc-result");
            Assert.AreEqual("return", record.DeviceId);
        }

        [TestMethod]
        public async Task FullRouting_ServerMode_TaskResultCanContinueOriginalPublish()
        {
            var recorder = new FullRoutingRecorder();
            using var services = BuildFullRoutingServices(recorder);
            await using var broker = await MqttTestBroker.StartAsync(services);
            await using var subscriber = await MqttTestClient.ConnectAsync(broker.Port, "full-routing-task-result-subscriber");
            await using var publisher = await MqttTestClient.ConnectAsync(broker.Port, "full-routing-task-result-publisher");
            var receivedTask = CaptureNextMessageAsync(subscriber);

            var subscription = await subscriber.SubscribeAsync("full/devices/return/task-ack");
            Assert.AreEqual(MqttClientSubscribeResultCode.GrantedQoS0, subscription.Items.Single().ResultCode);

            var publishResult = await publisher.PublishStringAsync("full/devices/return/task-ack", "ack");

            Assert.IsTrue(publishResult.IsSuccess, publishResult.ReasonString);
            var received = await ExpectMessageAsync(receivedTask);
            Assert.AreEqual("full/devices/return/task-ack", received.Topic);
            Assert.AreEqual("ack", received.ConvertPayloadToString());

            var record = await recorder.ExpectAsync("task-result");
            Assert.AreEqual("return", record.DeviceId);
        }

        [TestMethod]
        public async Task FullRouting_ServerMode_SuppressResultConsumesOriginalPublish()
        {
            var recorder = new FullRoutingRecorder();
            using var services = BuildFullRoutingServices(recorder);
            await using var broker = await MqttTestBroker.StartAsync(services);
            await using var subscriber = await MqttTestClient.ConnectAsync(broker.Port, "full-routing-suppress-subscriber");
            await using var publisher = await MqttTestClient.ConnectAsync(broker.Port, "full-routing-suppress-publisher");
            var receivedTask = CaptureNextMessageAsync(subscriber);

            var subscription = await subscriber.SubscribeAsync("full/devices/return/suppress");
            Assert.AreEqual(MqttClientSubscribeResultCode.GrantedQoS0, subscription.Items.Single().ResultCode);

            var publishResult = await publisher.PublishStringAsync("full/devices/return/suppress", "consume");

            Assert.IsTrue(publishResult.IsSuccess, publishResult.ReasonString);
            var record = await recorder.ExpectAsync("suppress-result");
            Assert.AreEqual("return", record.DeviceId);

            var completed = await Task.WhenAny(receivedTask, Task.Delay(TimeSpan.FromMilliseconds(300)));
            Assert.AreNotSame(receivedTask, completed, "Suppressed MQTT publish should not be delivered to subscribers.");
        }

        [TestMethod]
        public async Task FullRouting_FilterPipeline_AuthorizationFilterCanRejectBeforeAction()
        {
            var recorder = new FullRoutingRecorder();
            var filterRecorder = new FilterRecorder();
            using var services = BuildFullRoutingServices(
                recorder,
                options => options.AddMqttFilter(new RejectingAuthorizationFilter(filterRecorder)));
            await using var broker = await MqttTestBroker.StartAsync(services);
            await using var subscriber = await MqttTestClient.ConnectAsync(broker.Port, "full-routing-auth-subscriber");
            await using var publisher = await MqttTestClient.ConnectAsync(broker.Port, "full-routing-auth-publisher");
            var receivedTask = CaptureNextMessageAsync(subscriber);

            var subscription = await subscriber.SubscribeAsync("full/devices/42/telemetry");
            Assert.AreEqual(MqttClientSubscribeResultCode.GrantedQoS0, subscription.Items.Single().ResultCode);

            var publishResult = await publisher.PublishStringAsync("full/devices/42/telemetry", "{\"Temperature\":23.5}");

            Assert.IsTrue(publishResult.IsSuccess, publishResult.ReasonString);
            await filterRecorder.ExpectAsync("authorization");
            await AssertNoMessageAsync(receivedTask, "Rejected MQTT publish should not be delivered to subscribers.");
            Assert.IsFalse(recorder.TryGet("telemetry", out _));
        }

        [TestMethod]
        public async Task FullRouting_FilterPipeline_ActionFilterWrapsAction()
        {
            var recorder = new FullRoutingRecorder();
            var filterRecorder = new FilterRecorder();
            using var services = BuildFullRoutingServices(
                recorder,
                options => options.AddMqttFilter(new RecordingActionFilter(filterRecorder)));
            await using var broker = await MqttTestBroker.StartAsync(services);
            await using var publisher = await MqttTestClient.ConnectAsync(broker.Port, "full-routing-action-filter-publisher");

            var publishResult = await publisher.PublishStringAsync("full/devices/42/telemetry", "{\"Temperature\":23.5}");

            Assert.IsTrue(publishResult.IsSuccess, publishResult.ReasonString);
            await filterRecorder.ExpectAsync("action-before");
            var telemetry = await recorder.ExpectAsync("telemetry");
            await filterRecorder.ExpectAsync("action-after");
            Assert.AreEqual("42", telemetry.DeviceId);
            CollectionAssert.AreEqual(
                new[] { "1:action-before", "2:action-after" },
                filterRecorder.Events.ToArray());
        }

        [TestMethod]
        public async Task FullRouting_FilterPipeline_ResourceFilterWrapsAction()
        {
            var recorder = new FullRoutingRecorder();
            var filterRecorder = new FilterRecorder();
            using var services = BuildFullRoutingServices(
                recorder,
                options => options.AddMqttFilter(new RecordingResourceFilter(filterRecorder)));
            await using var broker = await MqttTestBroker.StartAsync(services);
            await using var publisher = await MqttTestClient.ConnectAsync(broker.Port, "full-routing-resource-filter-publisher");

            var publishResult = await publisher.PublishStringAsync("full/devices/42/telemetry", "{\"Temperature\":23.5}");

            Assert.IsTrue(publishResult.IsSuccess, publishResult.ReasonString);
            await filterRecorder.ExpectAsync("resource-before");
            var telemetry = await recorder.ExpectAsync("telemetry");
            await filterRecorder.ExpectAsync("resource-after");
            Assert.AreEqual("42", telemetry.DeviceId);
            CollectionAssert.AreEqual(
                new[] { "1:resource-before", "2:resource-after" },
                filterRecorder.Events.ToArray());
        }

        [TestMethod]
        public async Task FullRouting_FilterPipeline_DefaultExceptionFilterRejectsThrownAction()
        {
            var recorder = new FullRoutingRecorder();
            using var services = BuildFullRoutingServices(recorder);
            await using var broker = await MqttTestBroker.StartAsync(services);
            await using var subscriber = await MqttTestClient.ConnectAsync(broker.Port, "full-routing-default-exception-subscriber");
            await using var publisher = await MqttTestClient.ConnectAsync(broker.Port, "full-routing-default-exception-publisher");
            var receivedTask = CaptureNextMessageAsync(subscriber);

            var subscription = await subscriber.SubscribeAsync("full/devices/filters/throw");
            Assert.AreEqual(MqttClientSubscribeResultCode.GrantedQoS0, subscription.Items.Single().ResultCode);

            var publishResult = await publisher.PublishStringAsync("full/devices/filters/throw", "boom");

            Assert.IsTrue(publishResult.IsSuccess, publishResult.ReasonString);
            await AssertNoMessageAsync(receivedTask, "Default exception handling should reject the original publish.");
        }

        [TestMethod]
        public async Task FullRouting_FilterPipeline_ExceptionFilterCanRecoverWithResult()
        {
            var recorder = new FullRoutingRecorder();
            var filterRecorder = new FilterRecorder();
            using var services = BuildFullRoutingServices(
                recorder,
                options => options.AddMqttFilter(new AcknowledgeExceptionFilter(filterRecorder)));
            await using var broker = await MqttTestBroker.StartAsync(services);
            await using var subscriber = await MqttTestClient.ConnectAsync(broker.Port, "full-routing-exception-filter-subscriber");
            await using var publisher = await MqttTestClient.ConnectAsync(broker.Port, "full-routing-exception-filter-publisher");
            var receivedTask = CaptureNextMessageAsync(subscriber);

            var subscription = await subscriber.SubscribeAsync("full/devices/filters/throw");
            Assert.AreEqual(MqttClientSubscribeResultCode.GrantedQoS0, subscription.Items.Single().ResultCode);

            var publishResult = await publisher.PublishStringAsync("full/devices/filters/throw", "boom");

            Assert.IsTrue(publishResult.IsSuccess, publishResult.ReasonString);
            await filterRecorder.ExpectAsync("exception-handled");
            var received = await ExpectMessageAsync(receivedTask);
            Assert.AreEqual("full/devices/filters/throw", received.Topic);
            Assert.AreEqual("boom", received.ConvertPayloadToString());
        }

        [TestMethod]
        public async Task FullRouting_FilterPipeline_ResultFilterCanReplaceResult()
        {
            var recorder = new FullRoutingRecorder();
            var filterRecorder = new FilterRecorder();
            using var services = BuildFullRoutingServices(
                recorder,
                options => options.AddMqttFilter(new SuppressAcknowledgeResultFilter(filterRecorder)));
            await using var broker = await MqttTestBroker.StartAsync(services);
            await using var subscriber = await MqttTestClient.ConnectAsync(broker.Port, "full-routing-result-filter-subscriber");
            await using var publisher = await MqttTestClient.ConnectAsync(broker.Port, "full-routing-result-filter-publisher");
            var receivedTask = CaptureNextMessageAsync(subscriber);

            var subscription = await subscriber.SubscribeAsync("full/devices/return/task-ack");
            Assert.AreEqual(MqttClientSubscribeResultCode.GrantedQoS0, subscription.Items.Single().ResultCode);

            var publishResult = await publisher.PublishStringAsync("full/devices/return/task-ack", "ack");

            Assert.IsTrue(publishResult.IsSuccess, publishResult.ReasonString);
            await filterRecorder.ExpectAsync("result-replaced");
            var record = await recorder.ExpectAsync("task-result");
            Assert.AreEqual("return", record.DeviceId);
            await AssertNoMessageAsync(receivedTask, "Result filter should be able to suppress an acknowledged publish.");
        }

        [TestMethod]
        public async Task FullRouting_FilterPipeline_PayloadSizeLimitRejectsOversizedPublish()
        {
            var recorder = new FullRoutingRecorder();
            using var services = BuildFullRoutingServices(
                recorder,
                options => options.WithMaxPayloadSize(2));
            await using var broker = await MqttTestBroker.StartAsync(services);
            await using var subscriber = await MqttTestClient.ConnectAsync(broker.Port, "full-routing-size-limit-subscriber");
            await using var publisher = await MqttTestClient.ConnectAsync(broker.Port, "full-routing-size-limit-publisher");
            var receivedTask = CaptureNextMessageAsync(subscriber);

            var subscription = await subscriber.SubscribeAsync("full/devices/42/telemetry");
            Assert.AreEqual(MqttClientSubscribeResultCode.GrantedQoS0, subscription.Items.Single().ResultCode);

            var publishResult = await publisher.PublishStringAsync("full/devices/42/telemetry", "{\"Temperature\":23.5}");

            Assert.IsTrue(publishResult.IsSuccess, publishResult.ReasonString);
            await AssertNoMessageAsync(receivedTask, "Oversized MQTT publish should be rejected before action execution.");
            Assert.IsFalse(recorder.TryGet("telemetry", out _));
        }

        [TestMethod]
        public async Task FullRouting_ServerMode_RejectsInvalidGuidRouteValue()
        {
            var recorder = new FullRoutingRecorder();
            using var services = BuildFullRoutingServices(recorder);
            await using var broker = await MqttTestBroker.StartAsync(services);
            await using var publisher = await MqttTestClient.ConnectAsync(broker.Port, "full-routing-invalid-publisher");

            await publisher.PublishStringAsync("full/devices/not-a-guid/mode/online", "{}");

            Assert.IsFalse(recorder.TryGet("typed-route", out _));
            Assert.IsFalse(recorder.TryGet("wildcard", out _));
        }

        [TestMethod]
        public async Task SlimRouting_ServerMode_RoutesInterceptedPublishesByTopic()
        {
            var recorder = new SlimRoutingRecorder();
            using var services = BuildSlimRoutingServices(recorder);
            var dispatcher = services.GetRequiredService<IMqttApplicationMessageDispatcher>();

            await using var broker = await MqttTestBroker.StartPlainAsync();
            broker.Server.InterceptingPublishAsync += async args =>
            {
                var result = await dispatcher
                    .DispatchAsync(args.ApplicationMessage, args.ClientId, args.CancellationToken)
                    .ConfigureAwait(false);

                args.ProcessPublish = result.IsHandled;
            };

            await using var publisher = await MqttTestClient.ConnectAsync(broker.Port, "slim-routing-server-publisher");

            var publishResult = await publisher.PublishStringAsync("slim/devices/boiler/telemetry", "{\"Humidity\":44}");

            Assert.IsTrue(publishResult.IsSuccess, publishResult.ReasonString);

            var received = await recorder.ExpectAsync("telemetry");

            Assert.AreEqual("boiler", received.DeviceId);
            Assert.AreEqual(44, received.Humidity);
            Assert.AreEqual("slim-routing-server-publisher", received.ClientId);
            Assert.AreEqual("slim/devices/boiler/telemetry", received.Topic);
        }

        [TestMethod]
        public async Task SlimRouting_ClientMode_DispatchesReceivedMessagesByTopic()
        {
            var recorder = new SlimRoutingRecorder();
            using var services = BuildSlimRoutingServices(recorder);
            var dispatcher = services.GetRequiredService<IMqttApplicationMessageDispatcher>();

            await using var broker = await MqttTestBroker.StartPlainAsync();
            await using var subscriber = await MqttTestClient.ConnectAsync(broker.Port, "slim-routing-subscriber");
            await using var publisher = await MqttTestClient.ConnectAsync(broker.Port, "slim-routing-publisher");

            var subscription = await subscriber.SubscribeAsync("slim/devices/+/telemetry");
            Assert.AreEqual(MqttClientSubscribeResultCode.GrantedQoS0, subscription.Items.Single().ResultCode);

            subscriber.ApplicationMessageReceivedAsync += e => dispatcher.DispatchAsync(e);

            var publishResult = await publisher.PublishStringAsync("slim/devices/kitchen/telemetry", "{\"Humidity\":61}");

            Assert.IsTrue(publishResult.IsSuccess, publishResult.ReasonString);

            var received = await recorder.ExpectAsync("telemetry");

            Assert.AreEqual("kitchen", received.DeviceId);
            Assert.AreEqual(61, received.Humidity);
            Assert.AreEqual("slim-routing-subscriber", received.ClientId);
            Assert.AreEqual("slim/devices/kitchen/telemetry", received.Topic);
        }

        [TestMethod]
        public async Task SlimRouting_ClientMode_MarksUnmatchedReceivedMessagesAsFailed()
        {
            var recorder = new SlimRoutingRecorder();
            using var services = BuildSlimRoutingServices(recorder);
            var dispatcher = services.GetRequiredService<IMqttApplicationMessageDispatcher>();
            var message = MqttMessage("slim/devices/kitchen/unknown", "ignored");
            var eventArgs = new MqttApplicationMessageReceivedEventArgs(
                "slim-routing-publisher",
                message,
                publishPacket: MqttPublishPacket(message),
                acknowledgeHandler: (_, _) => Task.CompletedTask);

            var result = await dispatcher.DispatchAsync(eventArgs);

            Assert.IsFalse(result.IsHandled);
            Assert.IsFalse(eventArgs.IsHandled);
            Assert.IsTrue(eventArgs.ProcessingFailed);
            Assert.IsFalse(recorder.TryGet("telemetry", out _));
        }

        [TestMethod]
        public async Task SlimRouting_DirectMode_RoutesCompleteAndMiniMessagesByTopic()
        {
            var recorder = new SlimRoutingRecorder();
            using var services = BuildSlimRoutingServices(recorder);
            var dispatcher = services.GetRequiredService<IMqttApplicationMessageDispatcher>();

            var telemetry = await dispatcher.DispatchAsync(
                MqttMessage("slim/devices/line%2F1/telemetry", "{\"Humidity\":58}"),
                "direct-client");
            var ping = await dispatcher.DispatchAsync(
                MqttMessage("slim/devices/line%2F1/ping", "online"),
                "direct-client");
            var unmatched = await dispatcher.DispatchAsync(
                MqttMessage("slim/devices/line%2F1/missing", "ignored"),
                "direct-client");

            Assert.IsTrue(telemetry.IsHandled);
            Assert.IsTrue(ping.IsHandled);
            Assert.IsFalse(unmatched.IsHandled);

            var telemetryRecord = await recorder.ExpectAsync("telemetry");
            var pingRecord = await recorder.ExpectAsync("ping");

            Assert.AreEqual("line/1", telemetryRecord.DeviceId);
            Assert.AreEqual(58, telemetryRecord.Humidity);
            Assert.AreEqual("direct-client", telemetryRecord.ClientId);
            Assert.AreEqual("slim/devices/line%2F1/telemetry", telemetryRecord.Topic);

            Assert.AreEqual("line/1", pingRecord.DeviceId);
            Assert.AreEqual("online", pingRecord.PayloadText);
            Assert.AreEqual("direct-client", pingRecord.ClientId);
        }

        [TestMethod]
        public async Task SlimRouting_DirectMode_BindsGuidAndEnumRouteValues()
        {
            var recorder = new SlimRoutingRecorder();
            using var services = BuildSlimRoutingServices(recorder);
            var dispatcher = services.GetRequiredService<IMqttApplicationMessageDispatcher>();
            var deviceId = Guid.Parse("20e2def2-53df-49c7-91c5-a2f3df8f8d8b");

            var result = await dispatcher.DispatchAsync(
                MqttMessage($"slim/devices/{deviceId:D}/mode/online", "ignored"),
                "direct-client");

            Assert.IsTrue(result.IsHandled);
            Assert.IsTrue(result.ModelState.IsValid);

            var typedRecord = await recorder.ExpectAsync("typed");
            Assert.AreEqual(deviceId.ToString("D"), typedRecord.DeviceId);
            Assert.AreEqual("Online", typedRecord.PayloadText);
            Assert.AreEqual("direct-client", typedRecord.ClientId);
        }

        [TestMethod]
        public async Task SlimRouting_DirectMode_ReturnsModelStateForInvalidRouteValue()
        {
            var recorder = new SlimRoutingRecorder();
            using var services = BuildSlimRoutingServices(recorder);
            var dispatcher = services.GetRequiredService<IMqttApplicationMessageDispatcher>();

            var result = await dispatcher.DispatchAsync(
                MqttMessage("slim/devices/not-a-guid/mode/online", "ignored"),
                "direct-client");

            Assert.IsFalse(result.IsHandled);
            AssertModelStateError(result.ModelState, "deviceId", MqttBindingErrorCode.TypeConversionFailed);
            Assert.IsFalse(recorder.TryGet("typed", out _));
        }

        [TestMethod]
        public async Task SlimRouting_DirectMode_ReturnsModelStateForInvalidJsonPayload()
        {
            var recorder = new SlimRoutingRecorder();
            using var services = BuildSlimRoutingServices(recorder);
            var dispatcher = services.GetRequiredService<IMqttApplicationMessageDispatcher>();

            var result = await dispatcher.DispatchAsync(
                MqttMessage("slim/devices/boiler/telemetry", "{"),
                "direct-client");

            Assert.IsFalse(result.IsHandled);
            AssertModelStateError(result.ModelState, "$payload", MqttBindingErrorCode.PayloadDeserializationFailed);
            Assert.IsFalse(recorder.TryGet("telemetry", out _));
        }

        private static Task<MqttApplicationMessage> CaptureNextMessageAsync(MqttTestClient client)
        {
            var source = new TaskCompletionSource<MqttApplicationMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            client.ApplicationMessageReceivedAsync += args =>
            {
                source.TrySetResult(args.ApplicationMessage);
                return Task.CompletedTask;
            };

            return source.Task;
        }

        private static async Task<MqttApplicationMessage> ExpectMessageAsync(Task<MqttApplicationMessage> messageTask)
        {
            var completed = await Task.WhenAny(messageTask, Task.Delay(TimeSpan.FromSeconds(5))).ConfigureAwait(false);
            if (completed != messageTask)
            {
                Assert.Fail("Timed out waiting for MQTT message.");
            }

            return await messageTask.ConfigureAwait(false);
        }

        private static async Task AssertNoMessageAsync(Task<MqttApplicationMessage> messageTask, string failureMessage)
        {
            var completed = await Task.WhenAny(messageTask, Task.Delay(TimeSpan.FromMilliseconds(300))).ConfigureAwait(false);
            Assert.AreNotSame(messageTask, completed, failureMessage);
        }

        private static ServiceProvider BuildFullRoutingServices(
            FullRoutingRecorder recorder,
            Action<MqttRoutingOptions> configure = null)
        {
            return new ServiceCollection()
                .AddLogging(builder => builder.AddDebug())
                .AddSingleton(recorder)
                .AddMqttControllers<FullRoutingController>(options =>
                {
                    options.WithJsonSerializerContext(TestJsonContext.Default);
                    configure?.Invoke(options);
                })
                .BuildServiceProvider();
        }

        private static ServiceProvider BuildSlimRoutingServices(SlimRoutingRecorder recorder)
        {
            return new ServiceCollection()
                .AddSingleton(recorder)
                .AddMqttApplicationMessageSlimRouting(routes =>
                {
                    routes.MapJson(
                        "slim/devices/{deviceId}/telemetry",
                        TestJsonContext.Default.SlimTelemetryPayload,
                        static (context, payload) =>
                        {
                            var recorder = context.Services.GetRequiredService<SlimRoutingRecorder>();
                            recorder.Record(
                                "telemetry",
                                new SlimRouteRecord(
                                    context.GetRouteValue("deviceId"),
                                    payload.Humidity,
                                    null,
                                    context.ClientId,
                                    context.Message.Topic));

                            return ValueTask.CompletedTask;
                        });

                    routes.Map(
                        "slim/devices/{deviceId}/ping",
                        static context =>
                        {
                            var recorder = context.Services.GetRequiredService<SlimRoutingRecorder>();
                            recorder.Record(
                                "ping",
                                new SlimRouteRecord(
                                    context.GetRouteValue("deviceId"),
                                    null,
                                    context.Message.ConvertPayloadToString(),
                                    context.ClientId,
                                    context.Message.Topic));

                            return ValueTask.CompletedTask;
                        });

                    routes.Map(
                        "slim/devices/{deviceId}/mode/{mode}",
                        static context =>
                        {
                            var recorder = context.Services.GetRequiredService<SlimRoutingRecorder>();
                            var deviceId = context.GetRouteValue<Guid>("deviceId");
                            var mode = context.GetRouteValue<DeviceMode>("mode");
                            recorder.Record(
                                "typed",
                                new SlimRouteRecord(
                                    deviceId.ToString("D"),
                                    null,
                                    mode.ToString(),
                                    context.ClientId,
                                    context.Message.Topic));

                            return ValueTask.CompletedTask;
                        });
                })
                .BuildServiceProvider();
        }

        private static void AssertModelStateError(
            MqttModelStateDictionary modelState,
            string key,
            MqttBindingErrorCode errorCode)
        {
            Assert.IsFalse(modelState.IsValid);
            Assert.IsTrue(modelState.TryGetErrors(key, out var errors));
            Assert.IsTrue(errors.Any(error => error.ErrorCode == errorCode));
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

        private static MqttPublishPacket MqttPublishPacket(MqttApplicationMessage message)
        {
            return new MqttPublishPacket
            {
                Topic = message.Topic,
                Payload = message.Payload,
                QualityOfServiceLevel = message.QualityOfServiceLevel
            };
        }

        [MqttController]
        [MqttRoute("full/devices")]
        public sealed class FullRoutingController : MqttBaseController
        {
            private readonly FullRoutingRecorder _recorder;

            public FullRoutingController(FullRoutingRecorder recorder)
            {
                _recorder = recorder;
            }

            [MqttRoute("special/status")]
            public Task LiteralRouteWins()
            {
                _recorder.Record(
                    "literal",
                    new FullRouteRecord(
                        "special",
                        null,
                        Message.ConvertPayloadToString(),
                        ClientId,
                        Message.Topic,
                        Server));

                return Ok();
            }

            [MqttRoute("{deviceId:int}/telemetry")]
            public Task Telemetry(int deviceId, [FromPayload] FullTelemetryPayload payload)
            {
                _recorder.Record(
                    "telemetry",
                    new FullRouteRecord(
                        deviceId.ToString(),
                        payload.Temperature,
                        null,
                        ClientId,
                        Message.Topic,
                        Server));

                return Ok();
            }

            [MqttRoute("return/rpc")]
            public FullResponsePayload ReturnPayload([FromMqttPayload] FullTelemetryPayload payload)
            {
                _recorder.Record(
                    "rpc-result",
                    new FullRouteRecord(
                        "return",
                        payload.Temperature,
                        "rpc",
                        ClientId,
                        Message.Topic,
                        Server));

                return new FullResponsePayload
                {
                    Kind = "rpc",
                    Echo = payload.Temperature,
                    ClientId = ClientId
                };
            }

            [MqttRoute("return/task-ack")]
            public Task<MqttResult> ReturnTaskResult()
            {
                _recorder.Record(
                    "task-result",
                    new FullRouteRecord(
                        "return",
                        null,
                        "task-ack",
                        ClientId,
                        Message.Topic,
                        Server));

                return Task.FromResult<MqttResult>(Acknowledge());
            }

            [MqttRoute("return/suppress")]
            public MqttResult ReturnSuppressResult()
            {
                _recorder.Record(
                    "suppress-result",
                    new FullRouteRecord(
                        "return",
                        null,
                        "suppress",
                        ClientId,
                        Message.Topic,
                        Server));

                return Suppress();
            }

            [MqttRoute("filters/throw")]
            public MqttResult ThrowForFilter()
            {
                throw new InvalidOperationException("filter exception test");
            }

            [MqttRoute("{deviceId}/mode/{mode}/{revision:long?}")]
            public Task TypedRoute(Guid deviceId, DeviceMode mode, long? revision)
            {
                _recorder.Record(
                    revision.HasValue ? "typed-route-revision" : "typed-route",
                    new FullRouteRecord(
                        deviceId.ToString("D"),
                        null,
                        $"{mode}:{(revision.HasValue ? revision.Value.ToString() : "<null>")}",
                        ClientId,
                        Message.Topic,
                        Server));

                return Ok();
            }

            [MqttRoute("{deviceId}/r3")]
            public Task R3Binding(
                [FromMqttRoute("deviceId")] string deviceId,
                [FromMqttPayload] FullTelemetryPayload payload,
                [FromMqttClient] string clientId,
                [FromMqttUserProperty("trace-id")] string traceId,
                MqttRequestContext requestContext,
                MqttActionContext actionContext,
                MqttApplicationMessage message)
            {
                _recorder.Record(
                    "r3-binding",
                    new FullRouteRecord(
                        deviceId,
                        payload.Temperature,
                        $"{clientId}|{traceId}|{requestContext.Topic}|{ReferenceEquals(requestContext, actionContext.RequestContext)}|{ReferenceEquals(message, requestContext.Message)}",
                        ClientId,
                        Message.Topic,
                        Server));

                return Ok();
            }

            [MqttRoute("{*path}")]
            public Task Wildcard(string path)
            {
                _recorder.Record(
                    "wildcard",
                    new FullRouteRecord(path, null, Message.ConvertPayloadToString(), ClientId, Message.Topic, Server));

                return BadMessage();
            }
        }

        public sealed class FullRoutingRecorder : RouteRecorder<FullRouteRecord>
        {
        }

        public sealed class SlimRoutingRecorder : RouteRecorder<SlimRouteRecord>
        {
        }

        public sealed class FilterRecorder : RouteRecorder<string>
        {
            private int _sequence;
            private readonly ConcurrentQueue<string> _events = new ConcurrentQueue<string>();

            public IReadOnlyCollection<string> Events => _events.ToArray();

            public void RecordEvent(string key)
            {
                var value = $"{Interlocked.Increment(ref _sequence)}:{key}";
                _events.Enqueue(value);
                Record(key, value);
            }
        }

        public class RouteRecorder<TRecord>
        {
            private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
            private readonly ConcurrentDictionary<string, TaskCompletionSource<TRecord>> _records =
                new ConcurrentDictionary<string, TaskCompletionSource<TRecord>>(StringComparer.Ordinal);

            public void Record(string key, TRecord record)
            {
                GetSource(key).TrySetResult(record);
            }

            public bool TryGet(string key, out TRecord record)
            {
                if (_records.TryGetValue(key, out var source) && source.Task.IsCompletedSuccessfully)
                {
                    record = source.Task.Result;
                    return true;
                }

                record = default;
                return false;
            }

            public async Task<TRecord> ExpectAsync(string key)
            {
                var completed = await Task.WhenAny(GetSource(key).Task, Task.Delay(Timeout)).ConfigureAwait(false);
                if (completed != GetSource(key).Task)
                {
                    Assert.Fail($"Timed out waiting for route '{key}'.");
                }

                return await GetSource(key).Task.ConfigureAwait(false);
            }

            private TaskCompletionSource<TRecord> GetSource(string key)
            {
                return _records.GetOrAdd(
                    key,
                    _ => new TaskCompletionSource<TRecord>(TaskCreationOptions.RunContinuationsAsynchronously));
            }
        }

        public sealed class FullRouteRecord
        {
            public FullRouteRecord(
                string deviceId,
                double? temperature,
                string payloadText,
                string clientId,
                string topic,
                MqttServer server)
            {
                DeviceId = deviceId;
                Temperature = temperature;
                PayloadText = payloadText;
                ClientId = clientId;
                Topic = topic;
                Server = server;
            }

            public string DeviceId { get; }

            public double? Temperature { get; }

            public string PayloadText { get; }

            public string ClientId { get; }

            public string Topic { get; }

            public MqttServer Server { get; }
        }

        public sealed class SlimRouteRecord
        {
            public SlimRouteRecord(string deviceId, int? humidity, string payloadText, string clientId, string topic)
            {
                DeviceId = deviceId;
                Humidity = humidity;
                PayloadText = payloadText;
                ClientId = clientId;
                Topic = topic;
            }

            public string DeviceId { get; }

            public int? Humidity { get; }

            public string PayloadText { get; }

            public string ClientId { get; }

            public string Topic { get; }
        }

        public sealed class FullTelemetryPayload
        {
            public double Temperature { get; set; }
        }

        public sealed class FullResponsePayload
        {
            public string Kind { get; set; }

            public double Echo { get; set; }

            public string ClientId { get; set; }
        }

        public sealed class SlimTelemetryPayload
        {
            public int Humidity { get; set; }
        }

        public enum DeviceMode
        {
            Offline,
            Online
        }

        private sealed class RejectingAuthorizationFilter : IMqttAuthorizationFilter
        {
            private readonly FilterRecorder _recorder;

            public RejectingAuthorizationFilter(FilterRecorder recorder)
            {
                _recorder = recorder;
            }

            public ValueTask OnAuthorizationAsync(MqttAuthorizationFilterContext context)
            {
                _recorder.RecordEvent("authorization");
                context.Result = new MqttRejectResult(MqttPubAckReasonCode.NotAuthorized);
                return ValueTask.CompletedTask;
            }
        }

        private sealed class RecordingActionFilter : IMqttActionFilter
        {
            private readonly FilterRecorder _recorder;

            public RecordingActionFilter(FilterRecorder recorder)
            {
                _recorder = recorder;
            }

            public async ValueTask<MqttActionExecutedContext> OnActionExecutionAsync(
                MqttActionExecutingContext context,
                MqttActionExecutionDelegate next)
            {
                _recorder.RecordEvent("action-before");
                var executed = await next().ConfigureAwait(false);
                _recorder.RecordEvent("action-after");
                return executed;
            }
        }

        private sealed class RecordingResourceFilter : IMqttResourceFilter
        {
            private readonly FilterRecorder _recorder;

            public RecordingResourceFilter(FilterRecorder recorder)
            {
                _recorder = recorder;
            }

            public async ValueTask<MqttResourceExecutedContext> OnResourceExecutionAsync(
                MqttResourceExecutingContext context,
                MqttResourceExecutionDelegate next)
            {
                _recorder.RecordEvent("resource-before");
                var executed = await next().ConfigureAwait(false);
                _recorder.RecordEvent("resource-after");
                return executed;
            }
        }

        private sealed class AcknowledgeExceptionFilter : IMqttExceptionFilter
        {
            private readonly FilterRecorder _recorder;

            public AcknowledgeExceptionFilter(FilterRecorder recorder)
            {
                _recorder = recorder;
            }

            public ValueTask OnExceptionAsync(MqttExceptionContext context)
            {
                _recorder.RecordEvent("exception-handled");
                context.ExceptionHandled = true;
                context.Result = new MqttAcknowledgeResult("exception handled by test filter");
                return ValueTask.CompletedTask;
            }
        }

        private sealed class SuppressAcknowledgeResultFilter : IMqttResultFilter
        {
            private readonly FilterRecorder _recorder;

            public SuppressAcknowledgeResultFilter(FilterRecorder recorder)
            {
                _recorder = recorder;
            }

            public ValueTask<MqttResultExecutedContext> OnResultExecutionAsync(
                MqttResultExecutingContext context,
                MqttResultExecutionDelegate next)
            {
                if (context.Result is MqttAcknowledgeResult)
                {
                    _recorder.RecordEvent("result-replaced");
                    context.Result = new MqttSuppressResult("replaced by test result filter");
                }

                return next();
            }
        }

        [JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
        [JsonSerializable(typeof(FullTelemetryPayload))]
        [JsonSerializable(typeof(FullResponsePayload))]
        [JsonSerializable(typeof(SlimTelemetryPayload))]
        public sealed partial class TestJsonContext : JsonSerializerContext
        {
        }

        private sealed class MqttTestBroker : IAsyncDisposable
        {
            private MqttTestBroker(MqttServer server, int port)
            {
                Server = server;
                Port = port;
            }

            public MqttServer Server { get; }

            public int Port { get; }

            public static Task<MqttTestBroker> StartPlainAsync()
            {
                return StartAsync(null);
            }

            public static async Task<MqttTestBroker> StartAsync(ServiceProvider services)
            {
                var port = GetFreeTcpPort();
                var serverFactory = new MqttServerFactory();
                var serverOptions = new MqttServerOptionsBuilder()
                    .WithDefaultEndpoint()
                    .WithDefaultEndpointBoundIPAddress(IPAddress.Loopback)
                    .WithDefaultEndpointPort(port)
                    .Build();
                var server = serverFactory.CreateMqttServer(serverOptions);
                if (services != null)
                {
                    services.GetRequiredService<MqttRouter>();
                    services.GetRequiredService<MqttRouteTable>();
                    server.WithAttributeRouting(services, allowUnmatchedRoutes: false);
                }

                await server.StartAsync().ConfigureAwait(false);
                return new MqttTestBroker(server, port);
            }

            public async ValueTask DisposeAsync()
            {
                if (Server.IsStarted)
                {
                    await Server.StopAsync(new MqttServerStopOptionsBuilder().Build()).ConfigureAwait(false);
                }

                Server.Dispose();
            }

            private static int GetFreeTcpPort()
            {
                var listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                var port = ((IPEndPoint)listener.LocalEndpoint).Port;
                listener.Stop();
                return port;
            }
        }

        private sealed class MqttTestClient : IAsyncDisposable
        {
            private MqttTestClient(IMqttClient client)
            {
                Client = client;
            }

            public IMqttClient Client { get; }

            public event Func<MqttApplicationMessageReceivedEventArgs, Task> ApplicationMessageReceivedAsync
            {
                add => Client.ApplicationMessageReceivedAsync += value;
                remove => Client.ApplicationMessageReceivedAsync -= value;
            }

            public static async Task<MqttTestClient> ConnectAsync(int port, string clientId)
            {
                var clientFactory = new MqttClientFactory();
                var client = clientFactory.CreateMqttClient();
                var options = new MqttClientOptionsBuilder()
                    .WithClientId(clientId)
                    .WithTcpServer("127.0.0.1", port)
                    .WithCleanStart()
                    .Build();

                await client.ConnectAsync(options, CancellationToken.None).ConfigureAwait(false);
                return new MqttTestClient(client);
            }

            public Task<MqttClientPublishResult> PublishStringAsync(string topic, string payload)
            {
                return Client.PublishAsync(MqttMessage(topic, payload), CancellationToken.None);
            }

            public Task<MqttClientSubscribeResult> SubscribeAsync(string topic)
            {
                var options = new MqttClientSubscribeOptionsBuilder()
                    .WithTopicFilter(filter => filter
                        .WithTopic(topic)
                        .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce))
                    .Build();

                return Client.SubscribeAsync(options, CancellationToken.None);
            }

            public async ValueTask DisposeAsync()
            {
                if (Client.IsConnected)
                {
                    var options = new MqttClientDisconnectOptionsBuilder().Build();
                    await Client.DisconnectAsync(options, CancellationToken.None).ConfigureAwait(false);
                }

                if (Client is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
        }
    }
}
