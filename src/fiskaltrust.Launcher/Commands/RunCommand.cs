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
using fiskaltrust.Launcher.Extensions;

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
        private readonly ILifetime _lifetime;

        public RunCommandHandler(ILifetime lifetime)
        {
            _lifetime = lifetime;
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
                    services.AddSingleton(_ => LauncherConfiguration);
                    services.AddSingleton(_ => _lifetime);
                    services.AddSingleton(_ => _cashboxConfiguration);
                    services.AddSingleton(_ => new Dictionary<Guid, ProcessHostMonarch>());
                    services.AddSingleton<PackageDownloader>();
                    services.AddHostedService<ProcessHostMonarcStartup>();
                    services.AddSingleton(_ => Log.Logger);
                });

            builder.WebHost.ConfigureKestrel(options => HostingService.ConfigureKestrel(options, new Uri($"http://[::1]:{LauncherConfiguration.LauncherPort}")));

            builder.Services.AddCodeFirstGrpc();

            var app = builder.Build();

            app.UseRouting();
            app.UseEndpoints(endpoints => endpoints.MapGrpcService<ProcessHostService>());

            if (LauncherConfiguration.LauncherVersion is not null && Common.Constants.Version.CurrentVersion is not null)
            {
                var packageDownloader = app.Services.GetRequiredService<PackageDownloader>();
                SemanticVersioning.Version? launcherVersion = await packageDownloader.GetConcreteVersionFromRange(PackageDownloader.LAUNCHER_NAME, LauncherConfiguration.LauncherVersion, Constants.Runtime.Identifier);

                if (launcherVersion is not null && Common.Constants.Version.CurrentVersion < launcherVersion)
                {
                    if (LauncherConfiguration.LauncherVersion.ToString() == launcherVersion.ToString())
                    {
                        Log.Information("A new Launcher version is set.");
                    }
                    else
                    {
                        Log.Information("A new Launcher version is found for configured range \"{range}\".", LauncherConfiguration.LauncherVersion);
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

        public async Task StartLauncherUpdate(string? targetDir = null)
        {
            targetDir ??= Environment.ProcessPath;
            var executablePath = Path.Combine(LauncherConfiguration.ServiceFolder!, "service", LauncherConfiguration.CashboxId?.ToString()!, "fiskaltrust.Launcher");
            var process = new Process();
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.FileName = Path.Combine(executablePath, $"fiskaltrust.LauncherUpdater{(OperatingSystem.IsWindows() ? ".exe" : "")}");
            process.StartInfo.CreateNoWindow = false;

            process.StartInfo.Arguments = string.Join(" ", new string[] {
                "--launcher-process-id", Environment.ProcessId.ToString(),
                "--from", $"\"{Path.Combine(executablePath, $"fiskaltrust.Launcher{(OperatingSystem.IsWindows() ? ".exe" : "")}")}\"",
                "--to", $"\"{targetDir  ?? throw new Exception("Could not find launcher executable")}\"",
                "--launcher-configuration", $"\"{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(Serializer.Serialize(LauncherConfiguration, SerializerContext.Default)))}\"",
            });

            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.RedirectStandardOutput = true;

            process.Start();

            Log.Information("Launcher update starting in the background.");

            Thread.Sleep(10_000);

            if (process.HasExited)
            {
                Log.Error("Launcher Update failed. See {LogFolder} for the update log.", LauncherConfiguration.LogFolder);
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
