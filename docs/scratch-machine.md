# A scratch machine

Not built. An idea that came out of writing the automation plan, noted while the reasoning was
fresh. Read `docs/automation.md` first: the two lean on each other.

## What it is

A fader used as a needle on a record. The parameter says where the needle is in a recording,
from nought at the start to one at the end, and the sound comes out of moving it.

Not a scrub. Where the needle is does not make a sound; how fast it is moving does. Hold still
and there is silence. Move it slowly and the recording drawls. Move it backwards and it plays
backwards. That is what a hand on vinyl actually does, and it falls out of one subtraction:

```
rate = (position now - position last block) * samples in the recording / samples in the block
```

Notes are the second axis. A note sets a rate multiplier over the top, so the same drag on a
low note and on a high one are two different sounds. The fader says where, the note says how
fast that where is worth.

## Why it fits this application in particular

Three things it needs already exist and were built for other reasons.

**A fader already writes a parameter.** Pointing one at the position knob is the ordinary
gesture: Ctrl+Shift+M, rest the pointer, touch the fader. Nothing about the machine has to know
what a controller is.

**Automation already records exactly that stream.** A scratch is not a special recording format.
It is a lane of points against a parameter, which is what the automation plan describes, and
playing it back is the identical write arriving from the clock instead of from a hand. Build the
recording half of automation and this machine records itself.

**The machine's face is a described panel.** A fader, a take picker, a picture of the waveform
with the needle drawn on it. All of that exists in `Rack.Controls` and is spelled in `machine.json`
like every other machine.

## The two things that do not exist yet

**The voice has to interpolate between updates.** A controller sends around a hundred messages a
second. The engine renders forty eight thousand samples a second. Jumping the read pointer once
per message is one update every four hundred and eighty samples, and that is zipper noise rather
than a needle. The voice has to slew from the last position to the new one across the block,
which is also what gives it the inertia that makes it sound like a hand rather than a switch.

The rate for a block is therefore derived, not set: the distance travelled divided by the time
taken. A block where the position did not change renders silence, and that is correct.

**The write path is wrong for it.** `ControlTargets` coalesces writes and posts them to the
drawing thread, which is right for a filter knob and wrong for this. It adds a frame of latency
and, worse, throws away the intermediate positions, which are the material the sound is made
out of. This machine wants the parameter delivered straight to the voice on the MIDI thread,
with the panel following separately and at its own pace.

That is a real change to the shape of `IControlTarget`: something like a target that says it
wants every value rather than the last one per frame. Worth doing carefully, because everything
else is happier with the coalescing.

## A new engine, not a new panel

Every machine so far is a described panel over one of the engines the application already has:
sample, kit, sampler, synth, mono synth. This one is none of those, so it needs a kind of its
own and a voice of its own.

That is the piece `SoundMachineRegistry` already admits is missing: "A machine the app has no engine
for is read and ignored for now. That is the piece the contract still needs." A scratch machine
is the first real reason to finish it, and a good one to design against, because it is the
furthest thing from the existing five: no envelope, no note-on triggering a sound, no release.

## What it needs from a recording

A take, decoded once, read at an arbitrary and continuously varying position. `SampleStore`
already holds decoded audio and hands the same data to any number of voices, which is exactly
right: a needle is a position in shared data.

Reading backwards has to work, which the existing sample voices may not do. Worth checking
before promising anything.

## Effort, roughly

```
the voice: position in, slew, rate out, forward and backwards      a day
the parameter path that does not coalesce                          half a day, and some care
the kind, the instrument fields, the panel description             a day
```

The automation half is not counted here because it is the automation plan's, and this machine
is the reason to build the recording part of it early.

## Still open

- Whether the needle wraps or stops at the ends of the recording. Vinyl stops. Looping is more
  useful and less honest.
- Whether a note-off does anything at all. On a real deck the sound is the hand, and the note is
  only the speed, so probably not.
- Whether two of them can share one recording at different positions, which is two needles on
  one record and is either a lovely idea or an unplayable one.
