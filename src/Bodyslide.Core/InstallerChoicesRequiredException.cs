namespace Bodyslide.Core;

public sealed class InstallerChoicesRequiredException : IOException
{
    public IReadOnlyList<string> PluginNames { get; }
    public IReadOnlyList<string> Conflicts { get; }

    internal InstallerChoicesRequiredException(
        IReadOnlyList<string> pluginNames,
        IReadOnlyList<string> conflicts)
        : base("Installer choices must be resolved before conversion. "
            + "Multiple source variants of " + string.Join(", ", pluginNames)
            + " were found. Install the archive with MO2/Vortex and convert the selected installed mod, "
            + "or prepare a folder with one plugin/body variant plus its shared assets. "
            + "The converter will not choose or combine alternatives automatically.")
    {
        PluginNames = Array.AsReadOnly(pluginNames.ToArray());
        Conflicts = Array.AsReadOnly(conflicts.ToArray());
    }
}
