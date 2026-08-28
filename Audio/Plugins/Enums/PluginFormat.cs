namespace JingleBox2.Audio.Plugins.Enums;

/// <summary>Which plugin standard something speaks.</summary>
/// <remarks>
/// CLAP is first because it was here first, and because a saved chain from before VST3 existed
/// has no format written in it and has to read back as the one it was.
/// </remarks>
public enum PluginFormat
{
    /// <summary>CLAP, and the number a chain written before VST3 existed reads back as.</summary>
    Clap = 0,

    /// <summary>VST3, which is also the only format that can be an instrument here.</summary>
    Vst3 = 1
}
