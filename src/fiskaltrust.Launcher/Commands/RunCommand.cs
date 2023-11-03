using System.CommandLine;
using System.CommandLine.Invocation;
using fiskaltrust.Launcher.ProcessHost;
using fiskaltrust.Launcher.Services;
using Serilog;
using ProtoBuf.Grpc.Server;
using fiskaltrust.Launcher.Download;
using fiskaltrust.Launcher.Extensions;
using fiskaltrust.Launcher.Helpers;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace fiskaltrust.Launcher.Commands
{
    public class RunCommand : CommonCommand
    {
        public RunCommand(string name = "run", bool addCliOnlyParameters = true) : base(name, addCliOnlyParameters)
        {
            AddOption(new Option<int?>("--launcher-port"));
            AddOption(new Option<bool>("--use-offline"));
            AddOption(new Option<string?>("--service-folder"));
            AddOption(new Option<Uri?>("--configuration-url"));
            AddOption(new Option<Uri?>("--packages-url"));
            AddOption(new Option<string?>("--package-cache"));
            AddOption(new Option<Uri?>("--helipad-url"));
            AddOption(new Option<int?>("--download-timeout-sec"));
            AddOption(new Option<int?>("--download-retry"));
            AddOption(new Option<bool>("--ssl-validation"));
            AddOption(new Option<string?>("--proxy"));
            AddOption(new Option<string?>("--processhost-ping-period-sec"));
            AddOption(new Option<string?>("--cashbox-configuration-file"));
            AddOption(new Option<string?>("--tls-certificate-path"));
            AddOption(new Option<string?>("--tls-certificate-base64"));
            AddOption(new Option<string?>("--tls-certificate-password"));
            AddOption(new Option<string?>("--use-http-sys-binding"));
            AddOption(new Option<string?>("--use-legacy-data-protection"));
        }
    }

    public class RunCommandHandler : CommonCommandHandler
    {
        private readonly ILifetime _lifetime;
        private readonly SelfUpdater _selfUpdater;
        private readonly LauncherExecutablePath _launcherExecutablePath;

        public RunCommandHandler(ILifetime lifetime, SelfUpdater selfUpdater, LauncherExecutablePath launcherExecutablePath)
        {
            _lifetime = lifetime;
            _selfUpdater = selfUpdater;
            _launcherExecutablePath = launcherExecutablePath;
        }

        public new async Task<int> InvokeAsync(InvocationContext context)
        {
            if (await base.InvokeAsync(context) != 0)
            {
                return 1;
            }
            var builder = WebApplication.CreateBuilder();

            builder.Host
                .UseSerilog()
                .ConfigureServices((_, services) =>
                {
                    services.Configure<HostOptions>(opts => opts.ShutdownTimeout = TimeSpan.FromSeconds(30));
                    services.AddSingleton(_ => _launcherConfiguration);
                    services.AddSingleton(_ => _lifetime);
                    services.AddSingleton(_ => _cashboxConfiguration);
                    services.AddSingleton(_ => new Dictionary<Guid, IProcessHostMonarch>());
                    services.AddSingleton<PackageDownloader>();
                    services.AddHostedService<ProcessHostMonarcStartup>();
                    services.AddSingleton(_ => Log.Logger);
                    services.AddSingleton(_ => _launcherExecutablePath);
                });

            builder.WebHost.ConfigureBinding(new Uri($"http://[::1]:{_launcherConfiguration.LauncherPort}"), protocols: HttpProtocols.Http2);

            builder.Services.AddCodeFirstGrpc();

            var app = builder.Build();

            app.UseRouting();
#pragma warning disable ASP0014
            app.UseEndpoints(endpoints => endpoints.MapGrpcService<ProcessHostService>());
#pragma warning restore ASP0014

            await _selfUpdater.PrepareSelfUpdate(Log.Logger, _launcherConfiguration, app.Services.GetRequiredService<PackageDownloader>());

            try
            {
                await app.RunAsync(_lifetime.ApplicationLifetime.ApplicationStopping);

                await _selfUpdater.StartSelfUpdate(Log.Logger, _launcherConfiguration, LauncherConfigurationFile);
            }
            catch (Exception e)
            {
                Log.Error(e, "An unhandled exception occured.");
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
