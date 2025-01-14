using MQTTnet;
using MQTTnet.Extensions.ManagedClient;
using MQTTnet.Packets;
using System;
using System.Buffers;
using System.Collections.Generic;

namespace ExampleClient
{
    internal class Program
    {
        private static async System.Threading.Tasks.Task Main(string[] args)
        {
            var rnd = new Random();


            // Setup and start a managed MQTT client.
            var options = new MqttClientOptionsBuilder()
                .WithKeepAlivePeriod(TimeSpan.FromSeconds(5))
                .WithClientId($"Client{rnd.Next(0, 1000)}")
                .WithTcpServer("localhost")
                .WithCredentials("user", "password")
                .Build();

            var mqttClient = new MqttClientFactory().CreateMqttClient();

            mqttClient.ConnectingAsync += (e) =>
            {
                Console.WriteLine($"Connecting...");
                return System.Threading.Tasks.Task.CompletedTask;
            };

            mqttClient.ConnectedAsync += (e) =>
            {
                Console.WriteLine($"Connection Result: {e.ConnectResult.ResultCode}");
                return System.Threading.Tasks.Task.CompletedTask;
            };

            mqttClient.ApplicationMessageReceivedAsync += e =>
            {
                Console.WriteLine($"Message from {e.ClientId}: {e.ApplicationMessage.Payload.Length} bytes.");
                return System.Threading.Tasks.Task.CompletedTask;
            };

            await mqttClient.ConnectAsync(options);

            await mqttClient.SubscribeAsync(new MqttClientSubscribeOptions()
            {
                TopicFilters = new List<MqttTopicFilter> {
                    new MqttTopicFilter() {
                        Topic = "MqttWeatherForecast/90210/temperature",
                        QualityOfServiceLevel = MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce,
                    }
                }
            });

            await mqttClient.PublishAsync(new MqttApplicationMessage()
            {
                Topic = "MqttWeatherForecast/90210/temperature",
                QualityOfServiceLevel = MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce,
                Payload = new ReadOnlySequence<byte>(BitConverter.GetBytes(98.6d)),
            });

            await mqttClient.PublishAsync(new MqttApplicationMessage()
            {
                Topic = "asdfsdfsadfasdf",
                QualityOfServiceLevel = MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce,
                Payload = new ReadOnlySequence<byte>(BitConverter.GetBytes(100d)),
            });

            // StartAsync returns immediately, as it starts a new thread using Task.Run, and so the calling thread needs
            // to wait.
            Console.ReadLine();
        }
    }
}