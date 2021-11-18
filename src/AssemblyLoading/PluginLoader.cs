using System.Reflection;

namespace fiskaltrust.Launcher.AssemblyLoading
{
    public static class PluginLoader
    {
        public static T LoadPlugin<T>(string serviceFolder, string name)
        {
            string pluginLocation = Path.GetFullPath(Path.Combine(serviceFolder, name, $"{name}.dll"));

            var loadContext = new ComponentLoadContext(pluginLocation);
            var assembly = loadContext.LoadFromAssemblyName(new AssemblyName(Path.GetFileNameWithoutExtension(pluginLocation)));

            var type = assembly.GetTypes().FirstOrDefault(t => typeof(T).IsAssignableFrom(t)) ?? throw new Exception($"cloud not find {name} assembly");
            return (T)(Activator.CreateInstance(type) ?? throw new Exception("could not create Bootstrapper instance"));
        }
    }
}
