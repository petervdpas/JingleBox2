namespace JingleBox2.ViewModels;

/// <summary>
/// One control of a device and what it is set to, as the block in a track's chain prints it.
/// </summary>
/// <remarks>
/// The same two words whether the device is a plugin or one of our machines, because a chain
/// shows both side by side and a row that looked different depending on which it was would be
/// saying something that is not true: to a track they are the same thing, the box that makes or
/// works on the sound.
///
/// A record and not a view model. What a block prints is read again when something says it
/// moved, and the list is replaced rather than each row being told: three rows is not worth a
/// property changed apiece, and the plugin's own window is where a value is watched live.
/// </remarks>
/// <param name="Name">What the control is called, as the device names it.</param>
/// <param name="Text">Where it stands, already worded the way its own window words it.</param>
public sealed record DeviceReading(string Name, string Text);
