namespace JingleBox2.Waveform.Enums;

/// <summary>Which end of the region a gesture has hold of.</summary>
public enum TrimHandle
{
    /// <summary>Neither, which is what a click away from both handles means.</summary>
    None,

    /// <summary>The handle at the start of the region.</summary>
    Start,

    /// <summary>The handle at the end of it.</summary>
    End
}
