using fiskaltrust.Launcher.Common.Configuration;
using MQTTnet.Server;
using MQTTnet.AspNetCore;
using System.Text.Json;
using MQTTnet;

namespace fiskaltrust.Launcher.Services
{
    public class MessageBusService
    {
        private readonly LauncherConfiguration _launcherConfiguration;
        private readonly ILogger<MessageBusService> _logger;

        public MessageBusService(ILogger<MessageBusService> logger, LauncherConfiguration launcherConfiguration)
        {
            _launcherConfiguration = launcherConfiguration;
            _logger = logger;
        }

        public async Task<IHost> StartMQQTServer(CancellationToken token = default)
        {
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
                                });
                        });
                    });
            var app = builder.Build();
            await app.RunAsync(token);
            _logger.LogInformation("Started mqqt hosting. http://localhost:5000/mqqt ");
            return app;
        }
    }
}
