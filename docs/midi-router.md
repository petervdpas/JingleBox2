# One router

Everything a hand does to a note passes through one place, whatever produced it and wherever it
is going.

## Why

There are three paths today and they carry the same events.

- **Hardware in.** A port hands bytes to `MidiService`, a codec may rewrite them, `MidiDispatcher`
  applies the job the port was given in SETTINGS, and six routers turn a message into a note, a
  wheel, a knob, a pad or a transport key.
- **The drawn keyboard.** A mouse on a key, or a letter on the computer keyboard, is sounded by
  the panel that drew it. It tells `MidiMonitor` so the key lights, and that is all: nothing else
  in the program ever learns that a note was played.
- **Out.** `TrackMidiOut` writes notes to a port. Plugins are told separately, through
  `IPluginInstrument`, from the player.

So one gesture has two implementations depending on what made it, and a third for where it goes.
The cost is not theoretical. A key played on the hardware and the same key clicked on the screen
take different code to the same sound, so either can break while the other works, and neither can
be read out of one log. That is exactly the shape this codebase names everywhere else: **two ways
of doing one thing that answer differently**.

## The shape

One router. Everything that plays arrives at it, and everything that listens is told by it.

**In**

- a hardware port, after the codec and after the job the port was given
- a drawn keyboard: a mouse on a key
- the letter rows on the computer keyboard
- the drawn wheels, if they are ever given a hand
- the pattern, playing a song

**Out**

- our own voices, through the mixer
- a plugin, through `IPluginInstrument`
- a MIDI port, through `TrackMidiOut`
- the lights: which keys are down and where the wheels are

An event is a note on, a note off, a bend or a modulation, and it carries where it is going: a
track, or the loose bus a note played by hand goes onto.

## What it deletes

- the drawn keyboard sounding its own notes, which is the rule that keeps the two paths apart
- `MidiMonitor` standing in front of the note path as a special case, since the router is the
  monitor
- two answers to "which half of the application is in front", one in `TrackerNoteAdapter` and one
  in every panel that plays its own keys
- `ITrackNotes` and `ITrackWheels`, which were `IPlays` with the track named outright, so a track's
  own MIDI in had a contract apiece where every other source had one
- the second call every caller had to remember to make to the track's MIDI out

## What is hard, and has to be got right

- **Nothing may sound twice.** The rule that keeps the paths apart today is that a panel sounds
  its own key. Taking that away means the router must sound it instead, in the same instant, or a
  click feels late.
- **Two threads arrive.** A hardware note comes in on the port's thread and a click on the drawing
  thread. The router may not post either to the other, since a note posted to the drawing thread
  arrives at the frame rate.
- **A release must reach the half that took the press.** `TrackerNoteAdapter` already answers this
  and the answer moves into the router rather than being written a second time.
- **The pattern is a source too**, and it is the one that must not pay for any of this: it runs on
  the clock thread and already reaches the mixer directly.

## Order of work

1. The router and its contract, with the sources and sinks named, and no caller changed.
2. The drawn keyboard through it, which is the change that removes the second path.
3. The letter rows through it.
4. The wheels through it, replacing what is there now.
5. `TrackMidiOut` and the plugins as sinks rather than as separate calls from the player.

Each step leaves the program working, and each one deletes a path rather than adding one.

## Where it has got to

Step 1 is in: `IPlays` and `MidiRouter`, with `Tests/MidiRouterTests.cs` over them. Nothing is
wired to it yet, which is the point of the step: the contract can be read and argued with before
any path moves onto it.

Step 2 is under way, and step 4 arrived with it. The drawn wheels are sources now: `Wheel` takes
a drag, a scroll and a click, hands the new position back through its own command, and the host
takes that to the monitor, which is the door the hardware's wheel already goes through. A drawn
key was always a source; a drawn wheel is the same sentence, which is what settled it.

Step 2 is in. `SoundDeviceKeys` makes one call where it made two, through
`ISoundDevicePanel.Plays`: `PanelPlays` sounds the note on that panel's own instrument and the
monitor is told in the same breath. The drawn wheels beside the keyboard go down the same road, so
the keys and the wheels on a panel can no longer disagree about where they are going, which is
what they were doing.

Step 3 is in, and half of it was already done by step 2: a letter typed on a machine's panel goes
through `MachineKeys.Play`, so it moved with the mouse. What was left was the pattern's own letter
rows, which sounded a note and told nothing, so a drawn keyboard stayed dark while a part was
typed into it. `TrackerViewModel` is an `IPlays` now and `Plays` is its road.

Step 4 is in. The drawn wheels went onto the router when they became playable; the hardware ones
were still going the old way, through the monitor passing them on to the adapter, which is two
shapes for one gesture again. `MidiWheelRouter` tells the road now, `TrackerNoteAdapter` is an
`IPlays`, and `MainViewModel` builds the one road both the keys and the wheels from a port go
down: the half in front, and the monitor told in the same breath.

Step 5 is in, and it is smaller than it looked, because most of what the words describe was
already one road. A plugin is not a listener beside our own voices: it is reached through the
mixer, which is the one door `TrackerPlayer.Preview` and `Trigger` both go through, so what
decides whether a note lands on a plugin or on a voice of ours is the instrument and not the
router. The router deciding that would be the router becoming the sequencer.

What really was a separate call is the port. `Midi/PortPlays.cs` is the track's MIDI out standing
on the road, and `TrackerViewModel` holds one and puts it on beside the engine, which closed a
hole rather than tidying one: the pattern fed a track's MIDI out and so did a note arriving on
that track's own MIDI in, and the keyboard under somebody's hand did not. A track with a hardware
synth on its out and no instrument of its own was silent under the hands and played perfectly from
the pattern, which is one gesture answering two ways depending on what made it.

Two things fell out of it. A release is sent where its press went, since the hand names no track
and the cursor moves between the two halves of a key press; a track names itself and needs no
such memory. And the port is fed on the thread the key arrived on rather than after the post to
the drawing thread that writing a note into the pattern needs, so what leaves the port is no
longer waiting on a frame.

The track's own MIDI in went down the road in the same breath, which is what deleted the two
contracts: `MidiTrackRouter` takes an `IPlays` and names the track, so a key played into track
three now lights the drawn keyboards exactly as one played on the cursor's track does, and it
could not before.

**The wheels are deliberately not sent out of a port.** A note has an end and a wheel does not, so
a port left holding a bend is a synth holding every note off its own pitch with nothing anywhere
to straighten it, and who straightens it when a route changes or a song is opened has to be
settled first. That is its own piece of work: this step deletes paths and does not add one.
