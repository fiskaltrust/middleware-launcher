using System.CommandLine;
using System.CommandLine.Invocation;
using fiskaltrust.Launcher.ProcessHost;
using fiskaltrust.Launcher.Services;
using Serilog;
using ProtoBuf.Grpc.Server;
using fiskaltrust.Launcher.Download;
using Microsoft.Extensions.FileProviders;

namespace fiskaltrust.Launcher.Commands
{
    public class RunCommand : CommonCommand
    {
        public RunCommand() : base("run") {
            AddOption(new Option<int?>("--launcher-port"));
            AddOption(new Option<bool>("--use-offline"));
            AddOption(new Option<string?>("--service-folder"));
            AddOption(new Option<Uri?>("--packages-url"));
            AddOption(new Option<Uri?>("--helipad-url"));
            AddOption(new Option<int?>("--download-timeout-sec"));
            AddOption(new Option<int?>("--download-retry"));
            AddOption(new Option<bool>("--ssl-validation"));
            AddOption(new Option<string?>("--proxy"));
            AddOption(new Option<string?>("--processhost-ping-period-sec"));
            AddOption(new Option<string?>("--cashbox-configuration-file"));
        }
    }

    public class RunCommandHandler : CommonCommandHandler
    {
        private readonly CancellationToken _cancellationToken;

        public RunCommandHandler(IHostApplicationLifetime lifetime)
        {
            _cancellationToken = lifetime.ApplicationStopping;
        }

        public new async Task<int> InvokeAsync(InvocationContext context)
        {
            if(await base.InvokeAsync(context) != 0)
            {
                return 1;
            }

            var builder = WebApplication.CreateBuilder();
            builder.Host
                .UseSerilog()
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddSingleton(_ => _launcherConfiguration);
                    services.AddSingleton(_ => _cashboxConfiguration);
                    services.AddSingleton(_ => new Dictionary<Guid, ProcessHostMonarch>());
                    services.AddSingleton<PackageDownloader>();
                    services.AddSingleton<ProcessHostMonarcStartup>();
                    services.AddHostedService(provider => provider.GetRequiredService<ProcessHostMonarcStartup>());
                    services.AddSingleton(_ => Log.Logger);
                });

            builder.WebHost.ConfigureKestrel(options => HostingService.ConfigureKestrel(options, new Uri($"http://[::1]:{_launcherConfiguration.LauncherPort}")));

            builder.Services.AddCodeFirstGrpc();

            var app = builder.Build();

            app.UseRouting();
            app.UseEndpoints(endpoints => endpoints.MapGrpcService<ProcessHostService>());

            var guiBuilder = WebApplication.CreateBuilder();
            guiBuilder.Host.UseSerilog();

            guiBuilder.Services.AddControllersWithViews();
            guiBuilder.Services.AddDirectoryBrowser();

            guiBuilder.Host.ConfigureServices(services => {
                services.AddSingleton(_ => _launcherConfiguration);
                services.AddSingleton(_ => _cashboxConfiguration);
                services.AddSingleton(_ => app.Services.GetRequiredService<ProcessHostMonarcStartup>());
            });

            var guiApp = guiBuilder.Build();

            if (!guiApp.Environment.IsDevelopment())
            {
                guiApp.UseHsts();
            }


            var fileProvider = new PhysicalFileProvider(Path.Combine(Path.GetDirectoryName(Environment.ProcessPath!)!, "wwwroot")); // Use EmbeddedFileProveder
            
            guiApp.UseDefaultFiles(new DefaultFilesOptions { FileProvider = fileProvider });
            guiApp.UseStaticFiles(new StaticFileOptions { FileProvider = fileProvider });
            guiApp.UseRouting();

            guiApp.MapDefaultControllerRoute();
            guiApp.MapFallbackToFile("index.html");;

            try
            {
                await guiApp.StartAsync(_cancellationToken);
                await app.RunAsync(_cancellationToken);
                await guiApp.StopAsync();
            }
            catch(TaskCanceledException)
            {
                return 1;
            }
            catch (Exception e)
            {
                Log.Error(e, "An unhandled exception occured");
                return 1;
            }
            finally
            {
                Log.CloseAndFlush();
            }

            return 0;
        }
    }
}
