# Automation lanes

Built: the lanes, the clock that plays them, recording one off a knob, the strip that holds them
and the curve you draw on. What is not built is the typed view, which is a parameter column in
the pattern and waits on the column axis.

Checked against the Renoise 3.5.4 install on this machine on 2026-08-28, and revised where the
first draft had guessed. What changed is at the end of each section.

## Where it stands

Built, on 2026-08-28:

```
Tracker/AutomationLane.cs         the lane, its points, and what it says at a time
Tracker/AutomationPlayer.cs       the clock writing it, through IControlTargets
Tracker/AutomationRecorder.cs     a turned knob writing it down
Pattern.Lanes                     held by the pattern, moved, cleared, copied, undone
SongStore.LaneDocument            in and out of song.json
IControlTargets.On(track)         what a track has on it that could be automated
ViewModels/AutomationViewModel    one track's automation, and adding and clearing a lane
Views/AutomationStrip.axaml       the strip under the pattern, below the chain
Views/AutomationCurve.cs          the picture: the grid, the shape, and the points you drag
```

Two ways in. The automation handle under the chain folds open a strip where a head block chooses
the parameter and the room after it is that parameter's. Record knob movements in the pattern
menu arms the recorder, which is off by default and does nothing unless the song is playing.

Not built: the typed view, which is a parameter column in the pattern and shares its foundation
with note columns.

Fifty four tests, in `Tests/AutomationTests.cs` and two in `Tests/TrackerHistoryTests.cs`. The
file format is the half tested hardest, for the reason at the end of this page. The curve is not
among them: it is a Render pass and three pointer handlers, and what it draws is either right in
front of you or it is not.

## Why lanes and not more effect commands

The effect column is a letter and a byte: `V`, `P`, `R`, `A` and a parameter. It cannot address
a machine's parameters, because one letter cannot say which of twenty six knobs it means, and
one byte cannot carry both a parameter number and a value. Renoise solves that by adding a
device and parameter selector column, which costs pattern width on every track for something
most rows do not use.

A lane costs nothing until a track has one, and it is the shape the rest of the application
already fits.

## What Renoise stores, which is the plan already

The Lua API is the description; the schema is the file. `Schemas/RenoiseSong67.xsd:5378`:

```
PatternTrack
  Lines                     the cells
  Automations               PatternTrackAutomation
    Envelopes
      Envelope              PatternTrackEnvelope
        DeviceIndex
        ParameterIndex
        Envelope
```

One envelope per device parameter, per track, per pattern, addressed by a pair of numbers. That
is the storage this plan describes, so the storage half is not borrowed from a DAW. It is what a
tracker's own file looks like.

`Scripts/Types/renoise/library/renoise/song/pattern/automation.lua` fills in the rest:

```
EnvelopePoint    time in lines, value 0..1, scaling
Playmode         POINTS = 1, LINES = 2, CURVES = 3
length           read-only, always fits the pattern's length
points           unsorted allowed, no two points at one time
add_point_at / remove_point_at / clear_range / copy_from / has_point_at
```

Three decisions there are already made for us and should be taken as they stand.

**Values are normalised, 0 to 1.** A lane does not know whether it is driving hertz or decibels,
and does not have to: `IControlTarget` carries `Min` and `Max` and converts. It also means a
lane survives a machine changing a parameter's range in a later version.

**Automation belongs to the pattern, not to a song timeline.** Copying a pattern copies its
movement with it, which is the only behaviour that makes sense in a pattern sequencer, and it
is why a lane's length is the pattern's length rather than a number of its own.

**No two points at one time.** Which settles the recording question below, since a point at a
time that already has one is a replacement and there is nowhere for a second to go.

Corrected from the first draft: `scaling` belongs to `LINES`, not to `CURVES`. The API says it
plainly, "used in 'lines' playback mode only, 0.0 is linear", and the wiki agrees: a line
segment's handle bends it and controls its easing. `CURVES` is a cubic through the points and
needs no per-point field. So the cheap first implementation is `POINTS` and linear `LINES` with
scaling at zero, and the handles come later without a change to what is stored.

Also worth knowing before pricing sub-line time: Renoise quantises a point's time to 256 units
per line, and says what that unit is. "A time of 1.5 means: line 1 with a note column delay of
128." Sub-line automation and the delay column are one grid. `TrackerCell` here is note,
instrument, volume and effect, with no delay column, so both are missing together and either one
introduces the unit for the other.

## What Renoise does that we should not copy

Renoise has two ways to move a parameter and keeps both. The Automation List carries a small
icon per parameter saying which is in use, effect commands or envelopes **or both**, and the
skin has one bitmap for each: `Skin/Icons/Automation_Pattern.bmp` and `Automation_Envelope.bmp`.
Which of the two a recording lands in is a setting in the pattern editor's control panel rather
than a property of the parameter.

Those are two separate places in the file that both write one parameter and can disagree. The
"both" icon is a conflict indicator, which is what you build when the decision was not made. Do
not copy that part.

## One storage, two views

So: the points are the storage, and there is exactly one of them. How they are edited is a
separate question with two answers, and both are views onto the same list.

**The drawn view.** An envelope area under the pattern, points dragged with the mouse. This is
the only view that can show a recorded gesture, because a hand on a fader arrives at about a
hundred values a second and no column can hold or display that. It is also the one that costs
days.

**The typed view.** A parameter column in the pattern itself: a column whose header names the
target once, with values in the cells under it. That is what `TrackerCell.Volume` already is, a
byte whose meaning comes from which column it sits in. Line resolution, keyboard entry, select
a range and interpolate, copied and undone by the pattern's own machinery.

This is not Renoise's shape and should not be presented as if it were. Renoise's pattern-side
method is general effect commands carrying the device and parameter inside the command, which is
the width cost rejected at the top of this page. A column whose identity names the target pays
that cost once, in a header.

The reason it matters to sequencing rather than only to taste: a parameter column and a note
column are the same axis. Both make a track's column count variable, both change the pattern's
stride from `TrackCount` to a per track total, both need `"line:track:column:cell"` in the file,
both need `PatternMetrics.TrackWidth` per track and `TrackAt` as a walk rather than a division,
both move the selection's corners to flat columns, and both need `TrackerHistory` taught the new
shape. `docs/polyphony.md` prices that at five to six days as the cost of chords. It is the
foundation under two features.

## What is already in place

Most of the addressing and all of the writing exists, because remote control needed the same
two things.

```
IControlTarget          Name, Min, Max, Value, Set(double)
ControlTargets.Find     (machine, key) or (plugin, parameter), resolved against a track
```

The clock writing into a target at line 32 and a knob writing into it from CC 74 are the same
act against the same interface. A lane is that resolution plus a list of points.

What is not in place, and was missed in the first draft: nothing here can list the targets on a
track. `IControlTargets` has only `Find(mapping)`, deliberately asked per message because what a
mapping names moves underneath it. Renoise's Automation List is every parameter of every device
on the active track, searchable, with an "automated only" filter, and it is how a parameter gets
a lane in the first place. The parts exist a layer down, `MachineProject.Parameters` and a
plugin's own parameter list, so this is a new door on that interface rather than new knowledge.

## The pieces

**The lane, and the song that holds it.** Built. A type on `Pattern`, one per automated
parameter per track, naming what a `ControlMapping` names: machine and key, or plugin and
parameter number. `AutomationLane.Mapping()` is that correspondence and is the only place that
knows it, so the clock resolves a destination through the same code a knob does.

The first draft said one string per lane, the way a cell is one string. That turned out to be
the wrong half of the file to copy. A lane's header is half a dozen unlike fields, three of
which are only read for one kind of destination, and packing those into a string would mean
optional fields and a plugin id that must never contain the separator. Its points are the
opposite: hundreds of one identical shape, where a line each would make a recorded sweep a page
long. So the header is named fields and a point is `"time=value"`, which is compact where
compactness is worth having and legible where it is not. See `SongStore.LaneDocument`.

**The sequencer.** Built, as `AutomationPlayer`, called from the clock immediately before the
notes of the same line. That ordering is the whole of the question and it only has one answer: a
note landing on a line where the filter also moves should be played through the filter as the
line leaves it, not as the line before it left it.

`POINTS` holds the last value; `LINES` is a lerp between the surrounding points, with the
scaling field left at zero until the handles are built. Two things it does not do, both for the
same reason, which is that a write to a plugin is a round trip to another process: a value that
has not moved is not written again, and a lane's mapping is built once rather than per line.
The first line of a pass is written whatever the parameter holds, because where a hand left it
is not something a lane is entitled to assume.

**Recording, which was nearly free.** Built, as `AutomationRecorder`, hung on the one event
`MidiControlRouter` already raised for every value it writes. Everything it needed was there:
the link resolves the parameter, takeover has already stopped the value lurching when the hand
arrives, and the sensing has already worked out whether the control reports a position or a
movement. What was left was to put the number somewhere.

Two things it had to decide that the plan did not mention. The instant is read on the MIDI
thread and only the writing is handed to the drawing thread, because which line the song is on
has to be read as the message lands: posted whole, a fast hand would pile several values onto
whichever line the drawing thread woke up on. And a pass leaves one undo step per lane rather
than one per point, since a hand sweeping a filter across a pattern is one thing a person did
and a hundred and twenty points.

**The parameter list.** Built. `IControlTargets.On(track)` answers what a track has on it that
could be automated: the machine's parameters in panel order, then each insert's, then the strip,
which is the order a track is read in on the screen. The machine's own unsaved parameters are
left out, since a lane driving how much of the wave the picture shows would be a song insisting
on somebody's zoom level, and a plugin's read-only ones are left out because a gain reduction
meter reports rather than accepts.

It answers `ControlChoice` rather than a bare mapping: the device, the parameter's own name, and
what to ask `Find` for. The naming is worked out there while the machine and the plugin are in
hand, and asking again later would give a target's name, which is written for a status line and
ends in the track it is on. Forty rows all ending in the same three words is a list nobody can
scan.

It sits under the pattern, below the chain, and it is the same shape as the chain: a block at the
head saying which part you are working on, and the room after it given to that part. There the
head is the instrument and what follows is its effects; here the head is the parameter and what
follows is its lane. A person who has used one already knows where to look on the other.

Under the pattern because a lane is written against the pattern's own lines, which is where it is
drawn and where it is recorded from. It is about the track the cursor is in, the same track the
chain above it is about, and it follows the cursor through `FollowCursorTrack`, which is the one
place the chain already did.

Folded away behind a line that says "automation", and the chain above it folds the same way, by
`Views/FoldStrip.cs`: a `ContentControl` with a title and an open flag, holding whatever it is
given. One shape for both, because they are the same offer, a track's business taking room the
pattern would otherwise have and worth keeping only while you are working on it, and two
spellings of that would eventually disagree about which way the mark points. The line itself is
the machine editor's own fold, moved out to `App.axaml` when the tracker wanted it too: a chevron
that turns rather than swaps.

The chain starts open because a track always has one; the automation starts shut because a track
usually has none.

How much room each gets is dragged rather than decided, and each strip carries its own grip along
its top edge. That is the whole reason `FoldStrip` is a control rather than two rows of a grid
with a `GridSplitter` between them: a splitter shares one length out between the two rows it lies
between, so the automation's handle took its room off the chain above it and moving one moved the
other. A strip that owns its height answers only for itself, and the pattern, being the one thing
measured in what is left, gives up or takes back the difference without being asked.

The grip is a `Thumb`, drawn as the short bar the handle between two rows uses, because it is the
same gesture. A hairline alone would not do: along the bottom of a card that is exactly what the
card's own edge looks like, and it reads as the end of the thing above rather than as something
to take hold of.

The head block does not say which track. The pattern is on the screen above it, the cursor is in
the column it is about, and the track's number is already on the tab, on the status line and in
the pattern itself; the chain beside it had its own badge taken off for exactly that reason. It
carries a search box instead, which a machine's dozen parameters do not need and a plugin's two
hundred make unavoidable.

Adding a lane gives it one point, holding where the parameter stands. Renoise does the same and
it is the only useful answer: an empty lane says nothing, so the parameter would be listed as
automated and would not move.

It was in two wrong places first, and both are worth remembering. A page of its own, listing
every parameter of a track with a button on each row, which is a form to fill in rather than an
instrument to work on and somewhere you go instead of the music rather than something you open
beside it. Then per track in the mixer, with an AUTO button on every strip, which is where a
track's settings live but not where its lines are: a lane is written against lines, and the
lines are under the pattern.

**The typed view.** A parameter column, riding on the column axis described above. Cheap once
the axis exists, and the axis is note columns' bill.

**The drawn view.** Built, as `AutomationCurve`, in the room to the right of the head block. An
Avalonia control drawn in `Render`, like the pattern grid and the knobs, and reading its colours
through `ThemePalette` so a theme swap lands at once.

Time runs left to right although the pattern above it runs downwards, which looks like a
contradiction and is not: Renoise draws its automation the same way round. A curve is read as a
shape, and a shape a hand recognises rises and falls left to right. Turned on its side to match
the pattern it would be a shape nobody has ever read.

Click to add a point or take hold of one, drag to move it, right click to take one away, which
is the button this application already uses for taking a thing off a picture. Time snaps to
lines, since there is no finer grid to snap to; the value is free. A point dragged onto a line
that already has one is refused the move and keeps its change of value, because a lane holds one
point per time and a drag that swallowed its neighbours on the way past would destroy work while
going somewhere else. A gesture is one undo step and not one per movement, which is the rule the
recorder and the instrument knobs already follow.

The shape rests on the parameter's own nought rather than on the floor. A level runs from
silence upwards and its nothing is the floor; a pan runs from one side to the other and its
nothing is the middle, so a pan drawn as a level reads as hard left the whole way with a bump in
it, which is the opposite of what it says. Worked out from the target's own range rather than
from a list of which parameters are which, so a machine's pitch gets it without anybody saying
so.

Not on it: selecting a range of points, and dragging the handle between two points to bend that
segment, which is what `LINES` mode's scaling field is for.

## Effort

```
lane type, storage, sequencer                     done
target enumeration and the list panel             done
recording from a linked knob                      done
the typed view, given the column axis             a day
the drawn view                                    done, less the range and the segment handles
```

The column axis itself is not counted here. It is in `docs/polyphony.md` and it is two days plus
the interface work around it, spent once for note columns and parameter columns together.

## Order

Storage, sequencer, enumeration and recording are done, and that is the point at which movement
plays back with no editor at all. `docs/scratch-machine.md` was the reason to reach it early:
that machine records itself now that a knob's stream can be captured, and it is the only thing
here that makes the drawn view unavoidable rather than merely nice.

Next, and in this order: the range and the segment handles on the curve, which are the two
things it is missing and are hours apiece; then the column axis, which is `docs/polyphony.md`'s
bill and serves both features; the typed view on top of it; then note columns, which by then is
mostly entry rules and the mixer's per column cut.

## Decided already

- Lanes, not more effect commands. The column is out of room.
- Per pattern, per track, one lane per parameter. Renoise's shape, for Renoise's reason, and
  confirmed against its schema rather than inferred.
- Values normalised 0 to 1, converted through the target's own range.
- A lane names a machine and a parameter key, exactly as a `ControlMapping` does, so the same
  resolution serves both and a lane keeps working when a track's instrument is swapped for
  another of the same machine.
- One storage and two views, not two storages. Renoise has two and ships an icon to warn you
  when they overlap.
- `POINTS` and linear `LINES` first. `scaling` and `CURVES` are additions to the same points.
- No zooming out past the current pattern. It is the feature that makes people believe
  automation is a song timeline, it is display work with no model behind it, and it is the first
  thing to leave out.

## Answered by building it

- Recording overwrites rather than adds beside: the list holds one point per time, which is
  Renoise's rule, so a point where one already is replaces it. What that costs is the value that
  was there, and getting it back is the history's job. It does that: a pass is one step per lane.
- A lane belongs to a pattern and moves with its track. `Pattern.MoveTrack` renumbers them,
  `ClearTrack` takes them with the notes, a pattern made shorter drops the points past its end,
  and a track taken off takes its lane. All of that fell out of putting them on the pattern
  rather than beside it.
- Lanes had to be part of a pattern's undo step, and are. Left out, undo would have put the
  notes back and left the movement where it was, which is this codebase's recurring failure:
  doing nothing looks exactly like working.

## Still open

- Whether the handle should live in the chain's own leading block, beside the track number,
  rather than on a line of its own. It would save a line, which under the pattern is a line of
  music; against that, a word on its own line is easier to find than a button in a gutter.
- Whether the strip should follow the song while it is open rather than being read when the
  cursor changes track. It is read then and on the way in, and nothing else it is built out of
  says when it moves.

- Whether a lane can address a track's insert plugin as well as its instrument. The addressing
  supports it; the editor has to offer it, and a chain can be rearranged under a lane.
- What happens to a lane when the track's machine changes to a different one. The parameter key
  will not resolve, which is the same silence a link gets. Probably right, probably worth
  saying out loud on the page rather than leaving to be noticed.
- Whether a recorded pass should clear what it played over rather than only replacing the lines
  it touched. As built, a second pass across a lane leaves any point the hand did not land on
  exactly where it was, which is right for correcting a phrase and wrong for replacing one. The
  answer is probably a choice and not a rule, and neither can be offered without an editor.
- Whether sub-line points are worth having before there is a delay column, given that they are
  one grid and 256 units per line.
- Whether a lane is a column when both views exist. A parameter with a typed column and dragged
  points is one list of points seen twice, which is the point of the design, but the cursor has
  to be somewhere and two carets on one datum is a real interface question nobody has answered.
