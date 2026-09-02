using System.Collections.Generic;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JingleBox2.Audio.Plugins;
using JingleBox2.Rack.SoundDevices.Faces;
using JingleBox2.Rack.SoundDevices.Faces.Interfaces;
using JingleBox2.SoundDevices.SoundEffects;
using JingleBox2.SoundDevices.SoundEffects.Interfaces;
using JingleBox2.ViewModels.Interfaces;
using JingleBox2.ViewModels.Records;

namespace JingleBox2.ViewModels;

/// <summary>
/// One box in a chain that is one of ours: the effect, its engine, and its place in the row.
/// </summary>
/// <remarks>
/// The same block a plugin gets, drawn by the same template, because to a track they are the
/// same thing. What differs is behind it: this holds an engine of ours rather than somebody
/// else's program, so its readings are read straight out of it rather than fetched from another
/// process, and there is no state lump, no crash guard and no window belonging to a stranger.
///
/// A row rather than a thing, the same as a plugin's. The engine and its place in the chain both
/// belong to somebody else and this holds neither.
/// </remarks>
public sealed partial class SoundEffectViewModel : ObservableObject, IChainSlot, ISoundEffectShown
{
    /// <summary>How many of its controls the block prints, which is what a block has room for.</summary>
    private const int Readings = 3;

    /// <summary>The reading order of a face, so the three shown are the first three your eye lands on.</summary>
    private static readonly IPanelOrder Order = new PanelOrder();

    /// <summary>The chain this box is in, which is what a bypass or a move has to be told.</summary>
    private readonly PluginChainViewModel _chain;

    /// <summary>
    /// Makes a row for an effect of ours that is already running and already in the chain.
    /// </summary>
    /// <param name="chain">The chain this row is in, which is what a move is asked of.</param>
    /// <param name="effect">What the effect is: its name, its parameters and its face.</param>
    /// <param name="engine">The one that is doing the work, and holds what its knobs are at.</param>
    /// <param name="device">Its place in the chain, which is what carries the bypass.</param>
    public SoundEffectViewModel(
        PluginChainViewModel chain,
        SoundEffectProject effect,
        ISoundEffectEngine engine,
        PluginChain.Slot device)
    {
        _chain = chain;
        Effect = effect;
        Engine = engine;
        Device = device;

        _owner = chain.Target;
    }

    /// <summary>
    /// Where this box's chain lives, taken once when the row was built.
    /// </summary>
    /// <remarks>
    /// Taken rather than asked for, because the chain view under the pattern is pointed at
    /// whichever track the cursor is on and a row outlives that: asking it later would answer for
    /// wherever the cursor has since gone rather than for the chain this box is really on.
    /// </remarks>
    private readonly IChainOwner? _owner;

    /// <inheritdoc/>
    public string Id => Effect.Id;

    /// <inheritdoc/>
    public string Where => _owner?.Label ?? "";

    /// <summary>
    /// This box's face came to the front, so it is the one being worked on.
    /// </summary>
    /// <remarks>
    /// Which is what a link pointed at one of ours resolves against. A link names the effect and
    /// the key and never where it is standing, so something has to say which EchoBox, and the one
    /// whose face you are looking at is the only answer that is right on a track, on the master
    /// and on a pad alike. Nothing is applied by saying it.
    /// </remarks>
    public void InFront() => _chain.Front?.InFront(this);

    /// <summary>And has gone, so the track you are on answers again.</summary>
    public void NotInFront() => _chain.Front?.Gone(this);

    /// <summary>What the effect is, which is the face, the parameters and the name.</summary>
    public SoundEffectProject Effect { get; }

    /// <summary>The engine doing the work, which is also where its knobs stand.</summary>
    public ISoundEffectEngine Engine { get; }

    /// <inheritdoc/>
    public PluginChain.Slot Device { get; }

    /// <inheritdoc/>
    public string Name => Effect.Name.Length > 0 ? Effect.Name : Effect.Id;

    /// <inheritdoc/>
    /// <remarks>Ours, which is the one thing worth saying beside the name of something that is not a plugin.</remarks>
    public string Format => "JingleBox";

    /// <inheritdoc/>
    public string Vendor => Effect.Author;

    /// <inheritdoc/>
    /// <remarks>
    /// Straight off the engine, since it is in this process and holds its own values: a plugin's
    /// are worth keeping because reading one is a round trip to another process, and these are
    /// not.
    /// </remarks>
    public IReadOnlyList<ControlReading> Summary => _summary ??= Pick();

    /// <inheritdoc cref="Summary"/>
    private IReadOnlyList<ControlReading>? _summary;

    /// <inheritdoc/>
    public bool HasSummary => Summary.Count > 0;

    /// <inheritdoc/>
    public void Reread()
    {
        if (_summary is null) return;

        _summary = null;

        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(HasSummary));
    }

    /// <summary>The first few controls of the face, in the order the panel reads.</summary>
    private List<ControlReading> Pick()
    {
        var found = new List<ControlReading>(Readings);

        foreach (string key in Order.Of(Effect.Panel))
        {
            if (found.Count >= Readings) break;

            if (Effect.Parameters.Find(one => one.Key == key) is not { } parameter) continue;

            found.Add(new ControlReading(
                parameter.Name.Length > 0 ? parameter.Name : parameter.Key,
                Reads(Engine.ValueOf(key), parameter.Unit)));
        }

        return found;
    }

    /// <summary>A value as the block prints it: the number, and its unit when it has one.</summary>
    /// <param name="value">Where the control stands.</param>
    /// <param name="unit">What it is measured in, or nothing.</param>
    private static string Reads(double value, string unit)
    {
        string said = value.ToString("0.###", CultureInfo.InvariantCulture);

        return unit.Length > 0 ? said + " " + unit : said;
    }

    /// <inheritdoc/>
    public bool IsBypassed
    {
        get => Device.Bypassed;
        set
        {
            if (Device.Bypassed == value) return;

            Device.Bypassed = value;

            OnPropertyChanged();

            _chain.NotifyChanged();
        }
    }

    /// <inheritdoc/>
    [ObservableProperty]
    private bool _isOpen;

    /// <summary>The effect's face, as the one thing that draws it needs it.</summary>
    /// <remarks>Made once and kept: a panel redraws when it is handed a different face.</remarks>
    public Face Shown => _shown ??= new Face(Effect.Panel, Effect.Parameters, Effect.Folder);

    /// <inheritdoc cref="Shown"/>
    private Face? _shown;

    /// <summary>
    /// What its knobs stand at, which for one of ours is the engine itself.
    /// </summary>
    /// <remarks>
    /// Nothing in between and nothing to keep in step: the engine is in this process, it holds
    /// its own values and it is the thing the audio is going through. A knob turned on the face
    /// is heard on the next block.
    /// </remarks>
    public IPanelValues Values => _values ??= Watched(new SoundEffectValues(Engine));

    /// <inheritdoc cref="Values"/>
    private IPanelValues? _values;

    /// <summary>Hears what the face writes, so the readings on the block follow it.</summary>
    /// <param name="values">The values about to be handed to the panel.</param>
    private IPanelValues Watched(SoundEffectValues values)
    {
        values.Said += _ =>
        {
            Reread();

            _chain.NotifyChanged();
        };

        return values;
    }

    /// <summary>What its own Menu drops down: the surfaces pointed at this effect, and learning.</summary>
    public IPanelMenu Menu => _menu ??= new Midi.ControlMenu(() => Effect.Id, () => Name);

    /// <inheritdoc cref="Menu"/>
    private IPanelMenu? _menu;

    /// <inheritdoc/>
    public IRelayCommand RemoveCommand => new RelayCommand(() => _chain.Remove(this));

    /// <inheritdoc/>
    public IRelayCommand MoveLeftCommand => new RelayCommand(() => _chain.Move(this, -1));

    /// <inheritdoc/>
    public IRelayCommand MoveRightCommand => new RelayCommand(() => _chain.Move(this, 1));
}
