using fiskaltrust.Launcher.Common.Configuration;
using MQTTnet.Server;
using MQTTnet.AspNetCore;
using System.Text.Json;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using MQTTnet.Extensions.WebSocket4Net;
using Microsoft.AspNetCore.Hosting.Server;

namespace fiskaltrust.Launcher.Services
{
    public class MessageBusService : IDisposable
    {
        private readonly LauncherConfiguration _launcherConfiguration;
        private readonly ILogger<MessageBusService> _logger;
        private IManagedMqttClient _managedMqttClient;

        public MessageBusService(ILogger<MessageBusService> logger, LauncherConfiguration launcherConfiguration)
        {
            _launcherConfiguration = launcherConfiguration;
            _logger = logger;
        }

        public async Task<IHost> StartMQQTServer(CancellationToken token = default)
        {
            _managedMqttClient = await ConnectGlobalBusAsync();

            var builder = Host.CreateDefaultBuilder(Array.Empty<string>());
            builder.ConfigureWebHostDefaults(
                    webBuilder =>
                    {
                        webBuilder.UseKestrel(
                            o =>
                            {
                                o.ListenAnyIP(1883, l => l.UseMqtt());
                                o.ListenAnyIP(5000);
                            });
                        webBuilder.ConfigureServices(services =>
                        {
                            services.AddHostedMqttServer(
                             optionsBuilder =>
                             {
                                 optionsBuilder.WithDefaultEndpoint();
                             });

                            services.AddMqttConnectionHandler();
                            services.AddConnections();
                        });
                        webBuilder.Configure(app =>
                        {
                            app.UseRouting();
                            app.UseEndpoints(
                                endpoints =>
                                {
                                    endpoints.MapConnectionHandler<MqttConnectionHandler>(
                                        "/mqtt",
                                        httpConnectionDispatcherOptions => httpConnectionDispatcherOptions.WebSockets.SubProtocolSelector =
                                            protocolList => protocolList.FirstOrDefault() ?? string.Empty);
                                });
                            app.UseMqttServer(
                                server =>
                                {
                                    server.ValidatingConnectionAsync += async eventArgs =>
                                    {
                                        Console.WriteLine($"Client '{eventArgs.ClientId}' connected.");
                                        await Task.CompletedTask;
                                    };
                                    server.ClientConnectedAsync += async eventArgs =>
                                    {
                                        Console.WriteLine($"Client '{eventArgs.ClientId}' wants to connect. Accepting!");
                                        await Task.CompletedTask;
                                    };
                                    server.ClientSubscribedTopicAsync += async eventArgs =>
                                    {
                                        Console.WriteLine($"Client '{eventArgs.ClientId}' subscribed to topic '{eventArgs.TopicFilter.Topic}'. Accepting!");
                                        await Task.CompletedTask;
                                    };
                                    server.ClientDisconnectedAsync += async eventArgs =>
                                    {
                                        Console.WriteLine($"Client '{eventArgs.ClientId}' has disconnect. Accepting! ({System.Text.Json.JsonSerializer.Serialize(eventArgs)})");
                                        await Task.CompletedTask;
                                    };
                                    server.InterceptingPublishAsync += async eventArgs =>
                                    {
                                        await _managedMqttClient.EnqueueAsync(eventArgs.ApplicationMessage);
                                    };
                                });
                        });
                    });
            var app = builder.Build();
            await app.StartAsync(token);
            _logger.LogInformation("Started mqqt hosting. http://localhost:5000/mqqt ");
            return app;
        }

        public async Task<IManagedMqttClient> ConnectGlobalBusAsync()
        {
            var mqttFactory = new MqttFactory().UseWebSocket4Net();
            var mqttClient = mqttFactory.CreateManagedMqttClient();

            var mqttClientOptions = new MqttClientOptionsBuilder()
                .WithClientId(_launcherConfiguration.CashboxId + "-localbus")
                .WithWebSocketServer("gateway-sandbox.fiskaltrust.eu:80/mqtt")
                .Build();

            var managedMqttClientOptions = new ManagedMqttClientOptionsBuilder()
                .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
                .WithClientOptions(mqttClientOptions)
                .Build();

            mqttClient.ApplicationMessageReceivedAsync += async e =>
            {
                Console.WriteLine("Received application message.");
                // TODO republish on local messagebus
                e.DumpToConsole();
                await Task.CompletedTask;
            };
            mqttClient.ConnectionStateChangedAsync += async e =>
            {
                Console.WriteLine(JsonSerializer.Serialize(e));
                await Task.CompletedTask;
            };
            mqttClient.ConnectedAsync += async e =>
            {
                Console.WriteLine("Connected: args {0}", JsonSerializer.Serialize(e));
                await Task.CompletedTask;
            };
            mqttClient.DisconnectedAsync += async e =>
            {
                Console.WriteLine("Disconnected: {0}", JsonSerializer.Serialize(e));
                await Task.CompletedTask;
            };
            mqttClient.ConnectingFailedAsync += async e =>
            {
                await Task.CompletedTask;
            };
            await mqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic("#").Build().Topic);
            //await mqttClient.SubscribeAsync($"*");
            await mqttClient.StartAsync(managedMqttClientOptions);
            return mqttClient;
        }

        public void Dispose()
        {
            _managedMqttClient.Dispose();
        }
    }
}



public static class Helpers
{
    public static TObject DumpToConsole<TObject>(this TObject @object)
    {
        var output = "NULL";
        if (@object != null)
        {
            output = JsonSerializer.Serialize(@object, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }

        Console.WriteLine($"[{@object?.GetType().Name}]:\r\n{output}");
        return @object;
    }
}