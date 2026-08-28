namespace JingleBox2.Audio.Plugins.Enums;

/// <summary>What the host was doing with a plugin when everything stopped.</summary>
public enum PluginStage
{
    /// <summary>Opening or closing the plugin's own window.</summary>
    Window = 0,

    /// <summary>Loading the plugin at all, before any audio or any window.</summary>
    Load = 1
}
