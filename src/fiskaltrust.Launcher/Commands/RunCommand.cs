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
using fiskaltrust.Launcher.Common.Configuration;
using fiskaltrust.storage.serialization.V0;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
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
            AddOption(new Option<bool>("--use-http-sys-binding"));
            AddOption(new Option<bool>("--use-legacy-data-protection"));
            AddOption(new Option<bool>("--is-systemd-service"));
        }
    }

    public class RunOptions
    {

    }

    public class RunServices
    {
        public RunServices(ILifetime lifetime, SelfUpdater selfUpdater, LauncherExecutablePath launcherExecutablePath)
        {
            Lifetime = lifetime;
            SelfUpdater = selfUpdater;
            LauncherExecutablePath = launcherExecutablePath;
        }

        public readonly ILifetime Lifetime;
        public readonly SelfUpdater SelfUpdater;
        public readonly LauncherExecutablePath LauncherExecutablePath;
    }

    public static class RunHandler
    {
        public static async Task<int> HandleAsync(CommonOptions commonOptions, CommonProperties commonProperties, RunOptions _, RunServices runServices)
        {
            Log.Verbose($"RunHandler call");
            Log.Verbose($"runServices.LauncherExecutablePath {runServices.LauncherExecutablePath.Path}");
            var builder = WebApplication.CreateBuilder(new string[0]);
            Log.Verbose($"WebApplication.CreateBuilder()");
            builder.Host
                .UseSystemd()
                .UseSerilog()
                .ConfigureServices((_, services) =>
                {
                    services.Configure<Microsoft.Extensions.Hosting.HostOptions>(opts => opts.ShutdownTimeout = TimeSpan.FromSeconds(30));
                    services.AddSingleton(_ => commonProperties.LauncherConfiguration);
                    services.AddSingleton(_ => runServices.Lifetime);
                    services.AddSingleton(_ => commonProperties.CashboxConfiguration);
                    services.AddSingleton(_ => new Dictionary<Guid, IProcessHostMonarch>());
                    services.AddSingleton<PackageDownloader>();
                    services.AddHostedService<ProcessHostMonarcStartup>();
                    services.AddSingleton(_ => Log.Logger);
                    services.AddSingleton(_ => runServices.LauncherExecutablePath);
                });

            Log.Verbose($"RunHandler builder.Host");
            builder.WebHost.ConfigureBinding(new Uri($"http://[::1]:{commonProperties.LauncherConfiguration.LauncherPort}"), protocols: HttpProtocols.Http2);

            builder.Services.AddCodeFirstGrpc();

            var app = builder.Build();
            Log.Verbose($"RunHandler builder.Build");
            app.UseRouting();
#pragma warning disable ASP0014
            app.UseEndpoints(endpoints => endpoints.MapGrpcService<ProcessHostService>());
#pragma warning restore ASP0014
            Log.Verbose($"RunHandler PrepareSelfUpdate.call");
            try { 
                await runServices.SelfUpdater.PrepareSelfUpdate(Log.Logger, commonProperties.LauncherConfiguration, app.Services.GetRequiredService<PackageDownloader>()); 
            } catch (Exception e) { Log.Error(e, "An unhandled exception occured."); return 1;}
           Log.Verbose($"RunHandler PrepareSelfUpdate.end");
            try
            {
                await app.StartAsync(runServices.Lifetime.ApplicationLifetime.ApplicationStopping);

                await runServices.SelfUpdater.StartSelfUpdate(Log.Logger, commonProperties.LauncherConfiguration, commonOptions.LauncherConfigurationFile);
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
            Log.Verbose($"RunHandler end");
            return 0;
        }
    }
}
