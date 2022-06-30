using System.CommandLine;
using System.CommandLine.Invocation;
using fiskaltrust.Launcher.ProcessHost;
using fiskaltrust.Launcher.Services;
using Serilog;
using ProtoBuf.Grpc.Server;
using fiskaltrust.Launcher.Download;
using System.Reflection;
using System.Diagnostics;
using Serilog.Context;

namespace fiskaltrust.Launcher.Commands
{
    public class RunCommand : CommonCommand
    {
        public RunCommand() : base("run")
        {
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
        private bool _updatePending = false;
        private readonly CancellationToken _cancellationToken;

        public RunCommandHandler(IHostApplicationLifetime lifetime)
        {
            _cancellationToken = lifetime.ApplicationStopping;
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
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddSingleton(_ => _launcherConfiguration);
                    services.AddSingleton(_ => _cashboxConfiguration);
                    services.AddSingleton(_ => new Dictionary<Guid, ProcessHostMonarch>());
                    services.AddSingleton<PackageDownloader>();
                    services.AddHostedService<ProcessHostMonarcStartup>();
                    services.AddSingleton(_ => Log.Logger);
                });

            builder.WebHost.ConfigureKestrel(options => HostingService.ConfigureKestrel(options, new Uri($"http://[::1]:{_launcherConfiguration.LauncherPort}")));

            builder.Services.AddCodeFirstGrpc();

            var app = builder.Build();

            app.UseRouting();
            app.UseEndpoints(endpoints => endpoints.MapGrpcService<ProcessHostService>());

            if (_launcherConfiguration.LauncherVersion is not null && Constants.Version.CurrentVersion is not null && Constants.Version.CurrentVersion < _launcherConfiguration.LauncherVersion)
            {
                Log.Information("Launcher version {old} is outdated. Downloading new version {new}.", Constants.Version.CurrentVersion, _launcherConfiguration.LauncherVersion);

                try
                {
                    await app.Services.GetRequiredService<PackageDownloader>().DownloadLauncherAsync();
                    _updatePending = true;
                    Log.Information("Launcher will be updated to version {new} on shutdown.", Constants.Version.CurrentVersion, _launcherConfiguration.LauncherVersion);

                }
                catch (Exception e)
                {
                    Log.Error(e, "Cloud not download new Launcher version.");
                }
            }

            try
            {
                await app.RunAsync(_cancellationToken);

                if (_updatePending)
                {
                    StartUpdate();
                }
            }
            catch (TaskCanceledException)
            {
                return 1;
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

        private void StartUpdate()
        {
            var executablePath = Path.Combine(_launcherConfiguration.ServiceFolder!, "service", _launcherConfiguration.CashboxId?.ToString()!, "fiskaltrust.Launcher");
            var process = new Process();
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.FileName = Path.Combine(executablePath, $"fiskaltrust.LauncherUpdater{(OperatingSystem.IsWindows() ? ".exe" : "")}");
            process.StartInfo.CreateNoWindow = false;

            process.StartInfo.Arguments = string.Join(" ", new string[] {
                "--launcher-process-id", Environment.ProcessId.ToString(),
                "--from", $"\"{Path.Combine(executablePath, $"fiskaltrust.Launcher{(OperatingSystem.IsWindows() ? ".exe" : "")}")}\"",
                "--to", $"\"{Environment.ProcessPath ?? throw new Exception("Could not find launcher executable")}\"",
            });

            process.Start();

            Log.Information("Launcher update started in the background.");

            if(process.HasExited)
            {
                Log.Error("Launcher Update failed. See {} for the update log.", _launcherConfiguration.LogFolder);
            }
        }
    }
}
