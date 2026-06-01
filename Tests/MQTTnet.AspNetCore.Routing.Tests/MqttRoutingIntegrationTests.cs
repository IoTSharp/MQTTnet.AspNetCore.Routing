using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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

        private static ServiceProvider BuildFullRoutingServices(FullRoutingRecorder recorder)
        {
            return new ServiceCollection()
                .AddLogging(builder => builder.AddDebug())
                .AddSingleton(recorder)
                .AddMqttControllers<FullRoutingController>(options =>
                {
                    options.WithJsonSerializerContext(TestJsonContext.Default);
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
                })
                .BuildServiceProvider();
        }

        private static MqttApplicationMessage MqttMessage(string topic, string payload)
        {
            return new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce)
                .Build();
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

        public sealed class SlimTelemetryPayload
        {
            public int Humidity { get; set; }
        }

        [JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
        [JsonSerializable(typeof(FullTelemetryPayload))]
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
