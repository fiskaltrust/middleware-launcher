using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using System.Text.Json;

namespace fiskaltrust.Launcher.Services
{
    public class MessageBusClient : IDisposable
    {
        private IManagedMqttClient _managedMqttClient;
        private ManagedMqttClientOptions _managedMqttClientOptions;

        public async Task StartClientAsync()
        {
            await _managedMqttClient.StartAsync(_managedMqttClientOptions);
        }

        public MessageBusClient(string identifier)
        {
            var mqttFactory = new MqttFactory();
            _managedMqttClient = mqttFactory.CreateManagedMqttClient();
            var mqttClientOptions = new MqttClientOptionsBuilder()
                .WithClientId(identifier)
                .WithTcpServer("localhost")
                .Build();
            _managedMqttClientOptions = new ManagedMqttClientOptionsBuilder()
                .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
                .WithClientOptions(mqttClientOptions)
                .Build();
        }

        public void Dispose()
        {
            _managedMqttClient.Dispose();
        }


        public async Task PublishForCashBoxAsync(string cashboxid, string queueid, ifPOS.v1.ReceiptResponse response)
        {
            var applicationMessage = new MqttApplicationMessageBuilder()
             .WithTopic($"{cashboxid}/{queueid}/sign/response")
             .WithPayload(JsonSerializer.Serialize(response))
             .Build();
            await _managedMqttClient.EnqueueAsync(applicationMessage);
        }

        public async Task PublishSignAsync(string cashboxid, string queueid, ifPOS.v0.ReceiptRequest request, ifPOS.v0.ReceiptResponse response)
        {
            var applicationMessage = new MqttApplicationMessageBuilder()
             .WithTopic($"{cashboxid}/{queueid}/sign/response")
             .WithPayload(JsonSerializer.Serialize(new
             {
                 request,
                 response
             }))
             .Build();
            await _managedMqttClient.EnqueueAsync(applicationMessage);
        }

        public async Task PublishSignAsync(string cashboxid, string queueid, ifPOS.v1.ReceiptRequest request, ifPOS.v1.ReceiptResponse response)
        {
            var applicationMessage = new MqttApplicationMessageBuilder()
             .WithTopic($"{cashboxid}/{queueid}/sign/response")
             .WithPayload(JsonSerializer.Serialize(new
             {
                 request,
                 response
             }))
             .Build();
            await _managedMqttClient.EnqueueAsync(applicationMessage);
        }
    }
}
