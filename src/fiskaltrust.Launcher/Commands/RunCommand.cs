using System.CommandLine;
using System.CommandLine.Invocation;
using fiskaltrust.Launcher.ProcessHost;
using fiskaltrust.Launcher.Services;
using Serilog;
using ProtoBuf.Grpc.Server;
using fiskaltrust.Launcher.Download;
using fiskaltrust.Launcher.Extensions;
using fiskaltrust.Launcher.Helpers;
using Microsoft.AspNetCore.DataProtection;

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
        private bool _updatePending = false;
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

            builder.WebHost.ConfigureKestrel(options => HostingService.ConfigureKestrelForGrpc(options, new Uri($"http://[::1]:{_launcherConfiguration.LauncherPort}")));

            builder.Services.AddCodeFirstGrpc();

            var app = builder.Build();

            app.UseRouting();
            app.MapGrpcService<ProcessHostService>();

            if (_launcherConfiguration.LauncherVersion is not null && Common.Constants.Version.CurrentVersion is not null)
            {
                var packageDownloader = app.Services.GetRequiredService<PackageDownloader>();
                SemanticVersioning.Version? launcherVersion = await packageDownloader.GetConcreteVersionFromRange(PackageDownloader.LAUNCHER_NAME, _launcherConfiguration.LauncherVersion, Constants.Runtime.Identifier);

                if (launcherVersion is not null && Common.Constants.Version.CurrentVersion < launcherVersion)
                {
                    if (_launcherConfiguration.LauncherVersion.ToString() == launcherVersion.ToString())
                    {
                        Log.Information("A new Launcher version is set.");
                    }
                    else
                    {
                        Log.Information("A new Launcher version is found for configured range \"{range}\".", _launcherConfiguration.LauncherVersion);
                    }
                    Log.Information("Downloading new version {new}.", launcherVersion);

                    try
                    {
                        await packageDownloader.DownloadLauncherAsync(launcherVersion);
                        _updatePending = true;
                        Log.Information("Launcher will be updated to version {new} on shutdown.", launcherVersion);

                    }
                    catch (Exception e)
                    {
                        Log.Error(e, "Could not download new Launcher version.");
                    }
                }

            }

            try
            {
                await app.RunAsync(_lifetime.ApplicationLifetime.ApplicationStopping);

                if (_updatePending)
                {
                    await _selfUpdater.StartSelfUpdate(Log.Logger, _launcherConfiguration);
                }
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
