using System.CommandLine;
using System.CommandLine.Invocation;
using fiskaltrust.Launcher.ProcessHost;
using fiskaltrust.Launcher.Services;
using Serilog;
using ProtoBuf.Grpc.Server;
using fiskaltrust.Launcher.Download;
using System.Diagnostics;
using Serilog.Context;
using fiskaltrust.Launcher.Common.Helpers.Serialization;

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
                .ConfigureServices((_, services) =>
                {
                    services.Configure<HostOptions>(opts => opts.ShutdownTimeout = TimeSpan.FromSeconds(30));
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

            if (_launcherConfiguration.LauncherVersion is not null && Common.Constants.Version.CurrentVersion is not null)
            {
                var packageDownloader = app.Services.GetRequiredService<PackageDownloader>();
                SemanticVersioning.Version? launcherVersion = await packageDownloader.GetConcreteVersionFromRange(PackageDownloader.LAUNCHER_NAME, _launcherConfiguration.LauncherVersion, Constants.Runtime.Identifier);

                if (launcherVersion is not null && Common.Constants.Version.CurrentVersion < launcherVersion)
                {
                    Log.Information("A new Launcher version is configured. Downloading new version {new}.", _launcherConfiguration.LauncherVersion);

                    try
                    {
                        await packageDownloader.DownloadLauncherAsync(launcherVersion);
                        _updatePending = true;
                        Log.Information("Launcher will be updated to version {new} on shutdown.", _launcherConfiguration.LauncherVersion);

                    }
                    catch (Exception e)
                    {
                        Log.Error(e, "Could not download new Launcher version.");
                    }
                }

            }

            try
            {
                await app.RunAsync(_cancellationToken);

                if (_updatePending)
                {
                    await StartLauncherUpdate();
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

        private async Task StartLauncherUpdate()
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
                "--launcher-configuration", $"\"{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(Serializer.Serialize(_launcherConfiguration, SerializerContext.Default)))}\"",
            });

            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.RedirectStandardOutput = true;

            process.Start();

            Log.Information("Launcher update starting in the background.");

            Thread.Sleep(10_000);

            if (process.HasExited)
            {
                Log.Error("Launcher Update failed. See {LogFolder} for the update log.", _launcherConfiguration.LogFolder);
                var withEnrichedContext = (Action log) =>
                {
                    var enrichedContext = LogContext.PushProperty("EnrichedContext", " LauncherUpdater");
                    log();
                    enrichedContext.Dispose();
                };

                var stdOut = await process.StandardOutput.ReadToEndAsync();
                if (!string.IsNullOrEmpty(stdOut))
                {
                    withEnrichedContext(() => Log.Information(stdOut));
                }
                var stdErr = await process.StandardError.ReadToEndAsync();
                if (!string.IsNullOrEmpty(stdErr))
                {
                    withEnrichedContext(() => Log.Error(stdErr));
                }
            }
        }
    }
}
