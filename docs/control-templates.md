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

That pair is the unit, and it is what the page draws: one card each, headed with the thing
pointed at, the sort of thing it is, and the controller.

```
Templates
  OddSkilla   machine   nanoKONTROL2 · 10 controls        [Export...]
      Slider 1   attack
      Knob 1     duty
  Ouroboros   machine   MiniLab 3 · 4 controls            [Export...]
      Encoder 1  tune
```

Each card opens and folds away by its heading, the same chevron the machine editor's cards use.
They arrive folded, and one is open at a time: a card is ten or twenty rows, so a desk pointed at
six machines is a page nobody can hold in their eye, and folded the list is a heading apiece,
which is the shelf of templates and is what somebody opens this page to see.

Which card is open is the list's own answer rather than a flag on each card, since there is only
ever one, and it is held by the card's key. The list is thrown away and made afresh whenever
anything moves, so without that, laying a link down would fold up the card you are working in.

It was a card per target with a section per controller nested inside it. That drew the same
templates one level down and made the card a thing no file could be written from, since a card
holding two controllers would land on somebody who has one of them.

There was a second list above this one, the song's own: what you pointed at on an instrument on a
track or on a strip on the mixer went into that song's `.jibx`. Templates are what that was
reaching for and could not be. A copy of the same layout per song is the same work done again for
every song and can be handed to nobody, and which layer a link landed in depended on which of two
identical-looking panels the pointer happened to be over. Everything lands on the templates now.
What an older song is still holding is still read, and is still displaced by an arriving link, so
nothing laid down before this fights what is laid down today.

## Why it can be handed to somebody else

Every part of a link that decides anything is the same on every installation.

A **machine** is named by an id, and the id is what decides its engine, so it is the same id in
everybody's `instruments/` folder; a parameter is named by the key the machine stores it under,
which is the machine's own and travels in its zip. A **mixer** link names a strip and one of
`MixControl`'s six values, and strip three is strip three anywhere. The **transport** is four
keys and a cycle. There is no fourth: a plugin cannot be pointed at, for the reason under "A
plugin cannot be pointed at" below, and the `effect` word survives only so an older file
carrying one can be counted and left out.

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
kinds because to a knob they are one question in several vocabularies: a machine's own key, one
of the mixer's six words, or one of the transport's five. A field per kind would leave the rest
empty on every line. The fourth vocabulary, a plugin's parameter number, is still described here
because a file written before plugins were refused may carry it and is read far enough to say
how much was left out.

`control` is the legend on the front of the device and nothing is resolved from it. A device in
another of its programs sends different numbers under the same legends, so resolving by name
would quietly point at the wrong knob rather than at none. `track` is written only when a link
is nailed to a track, which is almost never, and a column of zeroes says nothing on every line
of a file meant to be read.

`ILinkTargets` is the one rule the page and the file both read. The cards are cut by it and the
file is written by it, so the two cannot drift into meaning different things by a target.

## Export and import

**Export** is on the card, because the card is the template. It was on a line inside the card
while a card could hold two controllers, and a file holding both would have landed on somebody
who has one of them.

**Import** is one button at the foot of the list.

The port is settled on the way in, which is the only conversion an import does. A controller
that is not plugged in keeps the name the file carried and its links wait for it, which is the
same rule a link already kept: a controller left in the other room is not a decision to unwire
it. That case is said out loud, because a template that applies perfectly and moves nothing
until the device arrives reads exactly like a file that failed to open.

Conflicts needed no new rule. `ControlLink.Take` lays a batch down by the rules a link made by
hand keeps: one control does one job, so an arriving link displaces whatever held its control
and whatever else was pointed at its target. Importing the same template twice therefore leaves
what once did. One act rather than a run of them, so the list is said to have changed once
rather than forty times and the settings are written once.

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
parameter key run together, so removing the key leaves the owner.

`Tests/ControlCardTests.cs` is the cutting: what shares a card, what may not, the order they
come in, both fallbacks, and a template naming a plugin being refused. `Tests/ControlTemplateTests.cs` is the file: the round trip, the
port being found by its profile's name and not by the port, a controller that is not here, the
mixer's strips, the transport's keys, links on two targets being refused, a newer version's
lines being counted, a damaged file, and an import repeated. `ControlLinksPageTests` in the same
file is the page's own half, since the file picker is the window's and everything either button
does once a path is known is the layer's.

## A plugin cannot be pointed at

A VST3 or a CLAP is somebody else's program, used by a song rather than owned by this
installation: picked as a track's instrument from the one list that merges the rack's machines
with the instrument plugins on this computer, or added to a track's chain as an effect. It is on
neither tab of the rack, and it cannot be pointed a hardware control at.

That is a decision, not a gap. **A plugin brings its own MIDI learn and keeps the result
itself.** A link made here would be a second mapping beside the plugin's own, in a different
place, with different rules about takeover and endless encoders, and nothing able to make the
two agree. So remote control is for machines, our own effects and the mixer, which are the
things this installation is the only owner of.

Ctrl+Shift+M on a plugin's window says so rather than doing nothing. `PluginWindow` answers the
keystroke itself instead of calling `LinkKey.Listen`, and swallows it, which is the opposite of
what `LinkKey` does with a keystroke it will not answer: there it is left alone because it may
mean something to whatever is in front of you, here it is being answered with a sentence. It
cannot be caught while the plugin's own interface has the keyboard, since those keys are
delivered to another program's window and never reach this one.

`LinkTargets.Point` refuses the `effect` word, so a template written before this carrying plugin
entries is counted and left out rather than failing the whole file, and `ControlLink` drops a
plugin link as it reads the settings.

### What was built, and why it went

It is worth writing down, because pointing at a plugin really did work and somebody will
otherwise build it again.

The host draws a knob per parameter behind the **Knobs** button in the plugin window's header,
and those are our controls, so a pointer can rest on one. For a plugin with a face of its own
that grid is not the answer, and it does not have to be: both standards report which parameter
you just touched, VST3 the moment you touch it through `IComponentHandler::performEdit`, CLAP at
the end of the block. So turning Vital's own Level knob, inside Vital's own window, offered
`Insert Vital Oscillator 1 Level`. Measured on the wire, not reasoned about.

What could never work is the other half: showing you it landed. There is no way to draw inside
another program's window. VST3 has no call asking a plugin to highlight a control. CLAP has one,
`param-indication`, which is no use to a VST3 and is the mirror of what a host would want anyway.
So the gesture had no confirmation at the place it was made.

The two readable things the formats do offer were never built, and are the ones to look at if
this is ever reopened. **CLAP `clap.remote-controls`**: the plugin declares named pages of eight
parameters, which is exactly a nanoKONTROL2 laid out on a plugin nobody has written a mapping
for. **VST3 `IMidiMapping`**: `getMidiControllerAssignment(bus, channel, cc, out ParamID)`, the
plugin's own declared MIDI remote, queryable and storable. Neither is in `Vst3Abi` or the CLAP
side here.

## Automation still points at a plugin

A lane names an insert on a track's chain, through the same `ControlKind.Insert` and the same
`ControlTargets.OnPlugin`, which is why neither was removed with the rest. It is a different
thing from a link and wants different answers: a lane is this song saying what a parameter does
over these lines, so it belongs to the song, it is not a fact about your hardware, and it has no
business being a template. A track's own instrument is deliberately not searched there, although
it may well be a plugin, because a lane names something on the chain and a plugin playing the
track is not on its chain.

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
`Views/Pointable.cs`, the machine panel and the transport bar each call `ControlLink.Offer`. It wants to be a subsystem of its own rather than a habit four views share,
and the reason is the same reason the templates wanted a file: recording a whole template in one
pass, control by control, is a thing the page should be able to drive rather than something that
only happens as a side effect of resting a pointer somewhere.
