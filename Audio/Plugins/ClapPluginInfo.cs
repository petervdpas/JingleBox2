namespace JingleBox2.Audio.Plugins;

/// <summary>
/// One plugin as it appears in a picker: what it is called, who made it, and enough to find
/// it again. The id is what a saved song stores, since a path moves between machines.
/// </summary>
public sealed record ClapPluginInfo(string Id, string Name, string Vendor, string Version, string Path)
{
    public override string ToString() => string.IsNullOrWhiteSpace(Vendor) ? Name : Name + " (" + Vendor + ")";
}
