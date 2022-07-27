namespace fiskaltrust.Launcher.AssemblyLoading
{

    public class PluginLoader
    {
        public T LoadComponent<T>(string path, Type[] sharedTypes)
        {
            var loader = McMaster.NETCore.Plugins.PluginLoader.CreateFromAssemblyFile(
                Path.GetFullPath(path),
                sharedTypes: sharedTypes,
                config => config.PreferSharedTypes = true);

            var type = loader.LoadDefaultAssembly().GetTypes().Where(t => typeof(T).IsAssignableFrom(t) && !t.IsAbstract).FirstOrDefault() ?? throw new Exception($"Could not load {nameof(T)} from {path}.");
            return (T?)Activator.CreateInstance(type) ?? throw new Exception($"Could not create {nameof(T)} instance.");
        }
    }
}
