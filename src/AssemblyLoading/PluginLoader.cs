using fiskaltrust.ifPOS.v1;
using fiskaltrust.ifPOS.v1.de;
using fiskaltrust.Launcher.Configuration;
using fiskaltrust.Middleware.Abstractions;
using McMaster.NETCore.Plugins;

namespace fiskaltrust.Launcher.AssemblyLoading
{

    public class PluginLoader
    {
        private readonly LauncherConfiguration _launcherConfiguration;
        public PluginLoader(LauncherConfiguration launcherConfiguration)
        {
            _launcherConfiguration = launcherConfiguration;
        }

        public T LoadComponent<T>(string package, Type[] sharedTypes)
        {
            var loader = McMaster.NETCore.Plugins.PluginLoader.CreateFromAssemblyFile(
                Path.GetFullPath(Path.Join(_launcherConfiguration.ServiceFolder, package, $"{package}.dll")),
                sharedTypes: sharedTypes,
                config => config.PreferSharedTypes = true);

            var type = loader.LoadDefaultAssembly().GetTypes().Where(t => typeof(T).IsAssignableFrom(t) && !t.IsAbstract).FirstOrDefault() ?? throw new Exception($"Could not load {nameof(T)} from {package}.dll");
            return (T?)Activator.CreateInstance(type) ?? throw new Exception($"Could not create {nameof(T)} instance");
        }
    }
}
