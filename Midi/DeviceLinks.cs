using JingleBox2.Midi.Enums;
using JingleBox2.Midi.Interfaces;

namespace JingleBox2.Midi;

/// <inheritdoc/>
public sealed class DeviceLinks : IDeviceLinks
{
    /// <inheritdoc/>
    public ControlMapping On(string? id, string? named, string? key) =>
        Made(ControlKind.Device, id, named, key);

    /// <inheritdoc/>
    /// <remarks>
    /// A jump rather than a pickup, because a press is a press: there is nothing to work out and
    /// nothing to pick up from. The words on the front read as words, since an action's key is
    /// written with underlines where a sentence would have spaces.
    /// </remarks>
    public ControlMapping Action(string? id, string? named, string? action)
    {
        var one = Made(ControlKind.Action, id, named, action);

        one.Pickup = ControlPickup.Jump;

        string said = (action ?? "").Replace('_', ' ');

        one.Name = one.Owner.Length > 0 ? one.Owner + " " + said : said;

        return one;
    }

    /// <summary>
    /// The mapping both of the above are, since they differ by one word.
    /// </summary>
    /// <remarks>
    /// <see cref="ControlScope.Focused"/> either way: a link on a device answers while that
    /// device is the one in front of you, which is a machine on the track you are on or an effect
    /// on that track's chain. That is what stops one knob meaning six things.
    ///
    /// The name is the owner and the control run together, which is what every place that makes
    /// a link has always written and what an older link is read back out of.
    /// </remarks>
    /// <param name="kind">A value to be moved, or something to be done.</param>
    /// <param name="id">The device's own id.</param>
    /// <param name="named">What it is called.</param>
    /// <param name="what">The parameter's key, or the action's word.</param>
    private static ControlMapping Made(ControlKind kind, string? id, string? named, string? what)
    {
        string owner = named ?? "";
        string said = what ?? "";

        return new ControlMapping
        {
            Kind = kind,
            Scope = ControlScope.Focused,
            Machine = id ?? "",
            Key = said,
            Owner = owner,
            Name = owner.Length > 0 ? owner + " " + said : said
        };
    }
}
