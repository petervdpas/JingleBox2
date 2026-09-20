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
