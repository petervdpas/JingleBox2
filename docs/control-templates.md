# Control templates

Built: the page, the file, the export and the import. This is the naming, what makes a template
portable, what the file looks like, and what is still open.

## The word

A knob can be pointed at a machine, at an effect on a track's chain, or at a mixer strip, and
the three needed one word between them. It matters because the word ends up in a file that
people hand to each other.

**Target.** It is what `IControlTarget` and `ControlTargets` have meant since the beginning: the
thing a control writes into. Nothing new was invented for it, and inventing one would have meant
two names for one thing living in the same feature.

What it is deliberately **not** called is a device. That is the industry word (Renoise, Bitwig
and Ableton all say device for a machine, an effect or a mixer strip) and it is the wrong word
here for one reason: this is a page about MIDI, where device already means the box on the desk.
The two ends of the wire may not share a name. So the hardware end is the **controller**
throughout, and `ControlDeviceLinks` was renamed `ControllerLinks` to stop it saying otherwise.

In the interface the umbrella is not shown at all. A card is headed with the thing itself and
the sort of thing is a quiet word beside it: `OddSkilla  machine`, `Serum 2  effect`,
`Track 3  mixer`, `Transport`. Nobody has to learn the umbrella to read the page, and the three
words that are shown are the three words a person already uses.

## A template is one controller against one target

That pair is the unit, and it is what the page now draws: a card per target, and inside it a
section per controller.

```
Templates
  OddSkilla   machine
    nanoKONTROL2 · 10 controls
      Slider 1   attack
      Knob 1     duty
  Ouroboros   machine
    MiniLab 3 · 4 controls
      Encoder 1  tune
```

Above the templates sits the song's own layer, which is the same drawing pointed at the other
list. What you point at on a machine on the rack works in every song and is a template; what you
point at on an instrument on a track is that song's and travels in its `.jibx`.

## Why it can be handed to somebody else

Every part of a link that decides anything is the same on every installation.

A **machine** is named by an id, and the id is what decides its engine, so it is the same id in
everybody's `instruments/` folder; a parameter is named by the key the machine stores it under,
which is the machine's own and travels in its zip. A **plugin** is named by the id the scanner
took from the plugin itself, which is the VST3 class id or the CLAP id, and its parameters are
numbered by the plugin. A **mixer** link names a strip and one of `MixControl`'s six values, and
strip three is strip three anywhere. The **transport** is four keys and a cycle.

Two things are not portable and both have an answer already.

The **port name** is not stable: one nanoKONTROL2 is `nanoKONTROL2 _ CTRL` to the ALSA sequencer
and `nanoKONTROL2 _ SLIDER/KNOB` to rawmidi, and Windows spells it a third way. A link stores
that string in `ControlMapping.Device`. A template must therefore name the **profile**, which is
already matched to a port by pattern in `Controllers/ControllerProfiles.cs`, and the port is
resolved on arrival. This is the one conversion an import has to do.

**Pickup and turn** are facts about the hardware rather than about the person, so they travel,
and a controller with a profile has them corrected on arrival anyway.

## The file

One template per file, `*.jbtl`, JSON, written whole through `SafeFile` so a half-written one
cannot replace a good one. The default folder is `templates/` under the application folder,
beside the machines and the controller profiles, because it is the same sort of thing: something
you own that arrived from outside. Only a default. A template can be written anywhere and opened
from anywhere, since the point of one is that it travels.

```json
{
  "jinglebox": "control-template",
  "version": 1,
  "controller": "nanoKONTROL2",
  "target": { "kind": "machine", "id": "machine.oddskilla", "name": "OddSkilla" },
  "controls": [
    { "control": "Slider 1", "channel": 1, "cc": 0, "parameter": "attack",
      "name": "OddSkilla attack", "pickup": "takeover", "turn": "offset" }
  ]
}
```

Every value is a word rather than a number out of an enum, so a template can be read, corrected
and sent on by somebody who has never seen this code. `parameter` is one field for all four
kinds because to a knob they are one question in four vocabularies: a machine's own key, a
plugin's parameter number, one of the mixer's six words, or one of the transport's five. A field
per kind would leave three empty on every line.

`control` is the legend on the front of the device and nothing is resolved from it. A device in
another of its programs sends different numbers under the same legends, so resolving by name
would quietly point at the wrong knob rather than at none. `track` is written only when a link
is nailed to a track, which is almost never, and a column of zeroes says nothing on every line
of a file meant to be read.

`ILinkTargets` is the one rule the page and the file both read. The cards are cut by it and the
file is written by it, so the two cannot drift into meaning different things by a target.

## Export and import

**Export** is on the controller's line inside a card rather than on the card, because that pair
is the template. A card can hold two controllers and a file holding both would land on somebody
who has one of them.

**Import** is one button under the Templates heading, and only there: a template is what one
controller does to one thing wherever it is met, which is the desk in as many words. A song's
layout is about this piece of music and is not a thing you receive.

The port is settled on the way in, which is the only conversion an import does. A controller
that is not plugged in keeps the name the file carried and its links wait for it, which is the
same rule a link already kept: a controller left in the other room is not a decision to unwire
it. That case is said out loud, because a template that applies perfectly and moves nothing
until the device arrives reads exactly like a file that failed to open.

Conflicts needed no new rule. `ControlLink.Take` lays a batch down by the rules a link made by
hand keeps: one control does one job, so an arriving link displaces whatever held its control
and whatever else was pointed at its target. Importing the same template twice therefore leaves
what once did. One act rather than a run of them, so the song's undo is told once, the lists are
said to have changed once, and the settings are written once.

What cannot be read is left out and counted rather than failing the lot. A template from a
newer version is mostly this version's, and the useful answer is the part that works plus a line
saying how much did not.

## What was built for it

`ControlMapping.Owner` is new: what a link is pointed at, in the words on the front of it. The
ids already said which thing; this is the same fact in a form a person reads, and it is separate
from `Name` because `Name` is the owner and the control run together and there is no way back to
the two halves once they are one string. Every place a link is made fills it in.

A link made before it existed carries no owner, and rather than heading its card `machine.oddskilla`
the name is read back out of it: every one of those names was written as the owner and the
parameter key run together, so removing the key leaves the owner. That works for machines and
not for plugins, whose parameter names are the plugin's and are not written down here, so an old
effect link keeps its id as a heading.

`Tests/ControlCardTests.cs` is the cutting: what shares a card, what may not, the order they
come in, and both fallbacks. `Tests/ControlTemplateTests.cs` is the file: the round trip, the
port being found by its profile's name and not by the port, a controller that is not here, the
mixer's strips, the transport's keys, links on two targets being refused, a newer version's
lines being counted, a damaged file, and an import repeated. `ControlLinksPageTests` in the same
file is the page's own half, since the file picker is the window's and everything either button
does once a path is known is the layer's.

## Still open

**Templates are not read on startup.** The folder is where they gather and where the picker
opens, and nothing walks it. Reading one lays links down, which is a thing that should happen
because somebody asked rather than because a file appeared. What might be worth having is a list
of what is in the folder, so importing is picking a name rather than finding a path.

**A machine that is not registered here.** A template naming one imports and its links wait,
exactly as they do for a controller that is not plugged in, and nothing says which machine it
was for. The situation is already answered elsewhere, where a song naming an unregistered
machine is read, passed over, and said once rather than left silent, and this should say the
same thing in the same words.

**Recording.** Learning a link is still a gesture spread across the panels that offer one:
`Views/Pointable.cs`, the machine panel, the plugin window and the transport bar each call
`ControlLink.Offer`. It wants to be a subsystem of its own rather than a habit four views share,
and the reason is the same reason the templates wanted a file: recording a whole template in one
pass, control by control, is a thing the page should be able to drive rather than something that
only happens as a side effect of resting a pointer somewhere.
