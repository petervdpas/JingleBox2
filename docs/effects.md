# Effects of our own

Decided on 2026-09-02. The rack has had an empty Effects tab since it was split in two, and this
is what goes in it. Of the five steps at the foot of this file, the first two are done and the
other two are not, and they were swapped: the designer came before the first engine, so the first
pedal is laid out in the tool rather than hand-written into a manifest. Nothing sounds yet, and
nothing shows on the rack's Effects tab yet either, which is the gate working rather than a thing
left out.

## An effect is not a machine

There were three words and now there are four. Engine, machine and instrument were the three,
and effect is the fourth: it sits beside machine rather than under it.

- An **engine** is compiled into the application. Its numbers are in people's files, so they
  never move and none is ever reused. Instrument engines are `TrackerInstrumentKind`; effect
  engines are a list of their own and do not join that one, because a number in it means "what
  plays this track" and an effect plays nothing.
- A **machine** is a face over an instrument engine. It takes notes and sounds them.
- An **effect** is a face over an effect engine. It takes a track's audio and hands it back
  changed. It has no keyboard, no zones, no pads and no kit, because it is never sent a note.
- An **instrument** is a machine in use: your name, your settings, kept with the song. An effect
  in use is a slot on a track's chain and takes **no name of its own**. Two of the same effect on
  one track read as that effect twice, which is what two of the same plugin already do and what a
  pedal board looks like.

It is its own world. Its own folder on disc, its own manifest, its own registry, its own project
type and its own page in the designer. Registered exactly the way a machine is, and by the same
rules, which are already written down for machines and hold here word for word: two folders, one
shipped beside the program and one under the application folder that decides; `offered.txt`, so
that removing an effect is not losing it and a newly shipped one still arrives; files kept up to
date against the shipped copy by each file's clock, with nothing ever deleted; and an id whose
engine this build has no engine for read off disc and passed over rather than put on the rack as
a box that cannot sound.

What the two worlds share is the drawing. A face is a face.

## The face is the effect's own, and a pedal is only one shape it takes

A stompbox is the picture to start from: a name plate, two or three knobs, a lamp and a
footswitch. It is not the rule. Plenty of effects are mostly picture, and the ones that are, are
that way for a reason: an EQ is a curve you drag, a compressor is a transfer curve with a dot
running about on it, and a filter with an analyser behind it says in one glance what six knobs
say in six readings. A format that only allowed a row of knobs would have decided against all of
those on the machine's behalf, which is the one thing this codebase keeps saying a face must
never have done to it.

So the face is described exactly as a machine's is and can be a pedal, a picture, or both, and
two things follow.

**A drawn picture is fed by the engine, not worked out by the panel.** That is already settled
for machines and the reasoning is on `IMachineScope`: what a wave is and what drive does to it is
the machine's business and lives where the engine lives, so the panel says how big the picture is
and how many points it wants and is handed the curve. An effect engine answers the same question
about its own curve, and an analyser is that answer taken from the audio going through rather
than from a note being sounded. Nothing new in kind, and it is the reason the contract is being
renamed rather than copied.

**The size an effect is drawn at is the host's business.** The pedal case wants the chain under
the pattern to be readable as a board, with each effect worth looking at without opening
anything, and the picture case wants a window. Those are one face at two sizes, which is what a
described panel already is: it names no pixels.

What is not settled, and is written under "Still open", is which drawn parts exist to be dropped
on a face. `Scope`, `Meter`, `Wave` and `Envelope` are in the library today and were each written
for an instrument. A curve somebody drags, which is what an EQ is, is a part nobody has needed
yet.

## The two parts an effect needs that a machine never did

**The footswitch is bypass, and bypass is not a parameter.** It is a fact about the slot on the
chain, `PluginChain.Device.Bypassed`, which is why it already survives being saved and restored
and why a plugin's own bypass parameter is deliberately not it. So it is a host-filled part in
exactly the way `Keys`, `Preset`, `Zones` and `Menu` are: the effect says where the switch goes
and how big it is, the host says what pressing it does. A new element kind, `Bypass`, and a new
contract for whoever fills it.

**The lamp beside it is the same fact read back.** `Led` is already in the part library, and a
lamp wired to bypass is the one lamp on a pedal that always means the same thing.

Both of those belong to the shared library rather than to the effect world, for the reason the
`Menu` part is shared: a machine may want a lamp saying whether it is in circuit one day, and a
part that only one world can use is a part written twice eventually.

## The rename

The parts an effect draws itself out of are the parts a machine draws itself out of, and they
are named for machines. `Rack.Abstractions` is published and everything public in it is a
promise, so this is a breaking change, and it is cheap exactly once: nothing outside this
repository ships against it yet.

The line to draw is not "does the word machine appear" but **do both worlds use it**. What both
use is renamed. What only the instrument world uses keeps the name it has, because there the
word is true.

Renamed, in `Rack.Abstractions`:

| Now | Becomes |
| --- | --- |
| `MachinePanel` | `Panel` |
| `MachineElement` | `PanelElement` |
| `MachineElementKinds` | `ElementKinds` |
| `MachineParameter` | `Parameter` |
| `IMachineValues` / `MachineValues` | `IPanelValues` / `PanelValues` |
| `IMachineMenu` | `IPanelMenu` |
| `MachineMenuItem` | `PanelMenuItem` |
| `MachineMenuCorners` | `MenuCorners` |
| `MachineMenuOptions` | `MenuOptionWords` |
| `IMachinePresets` | `IPanelPresets` |
| `IMachineScope` | `IPanelScope` |
| `MachineActions` | `PanelActions` |
| `MachineStarts` | `PanelStarts` |
| `MachineTheme` | `PanelTheme` |
| `MachineFace` | `Face` |

And in `Rack.Ui`:

| Now | Becomes |
| --- | --- |
| `MachinePanelView` | `PanelView` |
| `MachinePartSample` | `PartSample` |

Kept as they are, because only the instrument world has them: `IMachine`, `IMachineKeys`,
`IMachineNotes`, `MachineNotes`, `IMachinePads`, `IMachineZones`, `IMachineSlices`,
`IMachineTakes`, `IMachineLocation`, `IMachinePatch`, `IInstrumentName`. Everything in the
application's own assemblies that is genuinely about machines keeps its name too:
`MachineProject`, `MachineRegistry`, `MachineRack`, `MachineEditorViewModel` and the rest.

Two of those rows are the awkward ones and are worth saying out loud. `MachineMenuOptions` is
the words a menu can carry and `MenuOptions` is already taken by the rule that reads them, which
is why it becomes `MenuOptionWords` rather than the obvious thing. `MachineTheme` becomes
`PanelTheme` rather than `Theme`, since this application already has themes and they are the
thing a machine's colours are exempt from.

## On disc

`rack/effects/` beside `rack/machines/`, one folder to an effect, `effect.json` at the top of
it. Both worlds' folders sit under `rack/`, beside the program for what ships and under the
application folder for what this installation has, and what an installation already had at the
top of the app folder is carried in once. The same
shape as a machine's folder for the same reasons: `presets/` for what it ships with, `images/`
for what is drawn on it, and the whole folder is what travels as a zip. No `sounds/`, since an
effect plays nothing back.

The manifest is `EffectProject`, which is `MachineProject` without the parts an effect has no use
for: no starting-from, no engine borrowing, no sounds. Id, name, summary, author, version,
colours, parameters and a `Panel`.

Ids are `effect.<name>` the way machines are `machine.<name>`, and the id decides the engine
through the effect world's own register, which refuses an id it has no engine for.

## On the chain

`IAudioInsert` already says this in its own remarks: a plugin is one, and anything else that
wants a whole track rather than a voice can be another. So an effect of ours is an `IAudioInsert`
in the same `PluginChain`, in process, next to whatever plugins are on the track. Bypass, order
and the master's chain all work with nothing added.

What has to move is the writing down. `PluginChainState.Capture` walks the chain looking for
`IPluginEffect` and skips anything else, and `Restore` loads every entry through the plugin host.
So a chain entry needs to be able to say which of the two it is:

- `PluginDeviceConfig` gains an effect id, empty for a plugin, and the parameter values of one of
  ours are keyed by the effect's own parameter keys, which the existing
  `Dictionary<string, double>` already holds without changing shape.
- `Restore` reads the id first: ours is built from the register, a plugin goes to the plugin host
  exactly as now.
- A song written before this has no effect ids in it and reads back exactly as it did.

`TrackMix.Plugins` keeps its JSON name whatever the property is called, or every song on every
disc loses its chains.

## Remote control and automation

**Our own effects can be pointed at, and that was decided before they existed.** The rule is
already written: remote control is for machines, our own effects and the mixer, which are the
things this installation is the only owner of. A plugin stays refused, for the reason written up
under "A plugin cannot be pointed at": it brings its own MIDI learn and nothing can make the two
agree.

The template format has been holding the word `effect` open for this. `LinkTargets.Point` refuses
it today so that a file written before plugins were refused is counted and left out rather than
failing whole; what it will mean is one of ours. That is portable in the way a template has to
be: an effect id is the same id on everybody's disc and a parameter key is the effect's own and
travels in its zip, which is exactly what makes a machine link portable.

A link names the effect and which slot on the chain, the same pair `ControlMapping` already
carries for a plugin, and the fields for it are on that record already: `Machine` and `Key` for
what and which parameter, `Slot` for where. Automation goes the same way through
`ControlTargets`, which is one door for both.

The Menu part then means something on an effect's face on the day the face exists, which is what
it was built generic for.

## The six

Delay, filter, drive, reverb, EQ and compressor. Each is an engine and a face, and each engine is
a class that takes a block of interleaved stereo and works on it in place, under the rules on
`IAudioInsert`: no allocating, no blocking, no lock a hand can hold.

Two of them are mostly written already, as per-voice maths in `Tracker/Synth/`: the filters
(`ToneFilter`, `SweepFilter`) and the drive curve, which is also where the trap is recorded that
its makeup levels the curve at full scale and nowhere else. Per voice and per track are not the
same signal, so what moves across is the maths and not the class.

Reverb and the compressor are each a piece of work on their own and go last for that reason.

## The order of work

1. **The rename. Done**, and with it the namespaces and the assemblies. The types first, then
   `JingleBox2.Machines` became `JingleBox2.Rack.Faces` with `JingleBox2.Rack.Machines` beside it
   for what only an instrument has and `JingleBox2.Rack.Effects` waiting for what only an effect
   will have. The two assemblies are `JingleBox2.Rack.Abstractions` and `JingleBox2.Rack.Ui`,
   which is what `LICENSE.EXCEPTION` names. No warnings and the suite green at 1186. `Panel` collides with `Avalonia.Controls.Panel` and the collision is
   written up in `CLAUDE.md`: ours wins silently inside `JingleBox2.Rack.Ui`, since the
   enclosing namespace beats a using, and is an ambiguity anywhere else. Nothing on disc moved,
   which `Tests/MachinePartsTests.cs` says by reading the shipped manifests and passing
   untouched.
2. **The effect world with nothing in it. Done.** The folder rules came out first, since they
   were written for machines and are about neither: `IRackRegistry<T>` and `RackRegistry<T>` are
   the two folders, the offer, the bringing up to date and the engine gate, and `MachineRegistry`
   and `EffectRegistry` are what is left over when those are taken out. Then `EffectProject`
   (`effect.json`), `EffectProjects`, `IEffectEngines` as the gate with an empty table, and the
   rack's Effects tab drawn from what is registered, with no picker beside it: an effect cannot be
   shelved, because there is no box of yours to shelve. `Tests/EffectRackTests.cs` is eighteen
   tests and most of them are the refusals.
3. **One engine end to end**: the delay. On the chain, saved and read back in a song, its face
   drawn, bypass through the new part, pointable and automatable, with the tests including the
   refusals.
3. **The effects designer. Done**, and moved ahead of the engines so that a face is drawn in
   the tool rather than typed into a manifest by hand. One page told which world it is in:
   `IDesignWorld` is the handful of things that differ and `IDesignProject` is what the page
   edits, so the two are instances of one view model and one drawing, on two tabs inside
   DESIGNER rather than two pages along the top. New follows the tab it is pressed on, and what a
   machine is against what an effect is lives in the help, under a badge beside New. What came
   out on the way was everything that had been written for machines and was never about them: the
   pictures in a folder (`IPanelImages`), a folder carried whole (`IFolderCopy`), and the design
   history, which asked the project what type it is instead of naming the machine's.
   `Tests/EffectDesignerTests.cs` is the seam: which world makes which id, which pages are
   offered, and a folder of one kind refusing to open as the other.
4. **The delay.** The engine and the face are **done**: `Tracker/Effects/Delay.cs`, four knobs,
   measured in `Tests/DelayTests.cs`, and `rack/effects/EchoBox/effect.json` on the rack's
   Effects tab, with its own section in SETTINGS, System: imported from a zip, added back and
   thrown out exactly as a machine is, through the same archive and the same page. What is left of
   this step is the chain: putting one on a track, writing it into the song and reading it back,
   bypass through the new part, and pointing a controller or a lane at it. The rack showing an
   effect's face waits on that too, since a knob pointed at one has to have somewhere to land.
5. **The other five engines.**

## Still open

- **What the six are called.** A machine is not called Sampler, it is called Zampler, and a pedal
  wants the same treatment. Nothing about the design waits on it, but the folders do.
- Nothing about where the designer lives: both worlds are tabs inside DESIGNER, which is the one
  switch under Looks that already decides whether the workshop is shown at all.
- **Whether an effect can be pointed at while its face is not in front of you.** A machine link
  answers only while the track plays that machine, which is what stops knob one meaning six
  things. The equivalent for an effect is the track's chain holding it, and whether the slot has
  to match as well as the id is a question for when there are two of one pedal on a track.
- **An effect ships presets, and what a preset of one actually is has to be settled.** The
  question of whether was never really open: `IPanelPresets` is a face's contract rather than a
  machine's, `PanelStarts` already says where a picker's list comes from, and an effect's folder
  has `presets/` beside `images/` the same as a machine's. What differs is the file. A machine's
  preset is an **instrument** file, a whole `TrackerInstrument` with a name on it, because that
  is what a machine with settings is; an effect in use has no name and no instrument record, so
  its preset is the smaller thing, an effect id and its parameter values, and it wants a shape of
  its own rather than a `TrackerInstrument` with most of it left empty. Where the picker sits on
  a face is the other half: a preset browser across the top of a pedal stops it looking like a
  pedal, and a face that is mostly picture has nowhere obvious for one either.
- **A whole chain saved as one thing is a second kind of preset and is not this one.** A pedal
  board is the arrangement rather than any pedal in it, and it would have to name plugins as well
  as effects of ours. Worth wanting, worth keeping apart.
- **Which drawn parts an effect wants that no machine did.** A curve with handles on it, which is
  an EQ and is also how a compressor's knee is set, and an analyser reading the audio going past
  rather than a note being sounded. Both are `Scope` generalised rather than anything new in kind,
  and neither is needed for the first engine.
