# Automation lanes

Not built. This is the plan, written down while the reasoning was fresh, so that whoever
picks it up does not start from the beginning.

## Why lanes and not more effect commands

The effect column is a letter and a byte: `V`, `P`, `R`, `A` and a parameter. It cannot address
a machine's parameters, because one letter cannot say which of twenty six knobs it means, and
one byte cannot carry both a parameter number and a value. Renoise solves that by adding a
device and parameter selector column, which costs pattern width on every track for something
most rows do not use.

A lane costs nothing until a track has one, and it is the shape the rest of the application
already fits.

## What Renoise does, which is worth copying

From `renoise/song/pattern/automation.lua` in the 3.5.4 install:

```
renoise.PatternTrackAutomation   graphical automation of a device parameter within a pattern track

PatternTrack.automation[]        one lane per automated parameter, per pattern, per track
EnvelopePoint                    time in lines, plus a 1/256 sub-line grid; value 0..1; scaling
PlayMode                         Points | Lines | Curves
find_automation / create_automation / delete_automation
length                           always fits the pattern's length
```

Two decisions there are already made for us and should be taken as they stand.

**Values are normalised, 0 to 1.** A lane does not know whether it is driving hertz or decibels,
and does not have to: `IControlTarget` carries `Min` and `Max` and converts. It also means a
lane survives a machine changing a parameter's range in a later version.

**Automation belongs to the pattern, not to a song timeline.** Copying a pattern copies its
movement with it, which is the only behaviour that makes sense in a pattern sequencer, and it
is why a lane's length is the pattern's length rather than a number of its own.

## What is already in place

Most of the addressing and all of the writing exists, because remote control needed the same
two things.

```
IControlTarget          Name, Min, Max, Value, Set(double)
ControlTargets.Find     (machine, key) or (plugin, parameter), resolved against a track
```

The clock writing into a target at line 32 and a knob writing into it from CC 74 are the same
act against the same interface. A lane is that resolution plus a list of points.

## The pieces

**The lane, and the song that holds it.** A type on `Pattern`, one per automated parameter per
track, naming what a `ControlMapping` names: machine and key, or plugin and parameter number.
Serialised into `song.json` the way cells already are, one string per lane, so a song stays
readable and diffable. See `SongStore.PatternDocument`.

**The sequencer.** At each line, and at sub-line resolution where a lane has points between
lines, evaluate every lane on the track and write through the target. Interpolation for `Lines`
is a lerp between the surrounding points; `Points` holds the last value; `Curves` needs the
scaling field.

**Recording, which is nearly free.** A knob that is already linked, with the transport armed,
appends a point at the playing line instead of only setting the value. `ControlSense` has
already worked out what kind of control it is, and takeover already stops the value lurching
when playback crosses an existing point. This is the part that will feel like magic for the
effort it costs, and it should be built early rather than last.

**The editor, which is all of the work.** A lane area under the pattern: choosing which
parameter a lane is about, drawing and dragging points, selecting a range, showing which
parameters already have lanes, and the play mode switch. None of it is deep and there is a lot
of it.

## Effort

```
lane type, storage, sequencer     about a day
recording from a linked knob      hours, on top of the above
the editor                        three to five days, and it is all interface
```

## Decided already

- Lanes, not more effect commands. The column is out of room.
- Per pattern, per track, one lane per parameter. Renoise's shape, for Renoise's reason.
- Values normalised 0 to 1, converted through the target's own range.
- A lane names a machine and a parameter key, exactly as a `ControlMapping` does, so the same
  resolution serves both and a lane keeps working when a track's instrument is swapped for
  another of the same machine.

## Still open

- Whether a lane can address a track's insert plugin as well as its instrument. The addressing
  supports it; the editor has to offer it, and a chain can be rearranged under a lane.
- What happens to a lane when the track's machine changes to a different one. The parameter key
  will not resolve, which is the same silence a link gets. Probably right, probably worth
  saying out loud on the page rather than leaving to be noticed.
- Whether recording overwrites points under the playhead or adds beside them. Overwriting is
  what a hand expects; adding is what an undo can survive.
