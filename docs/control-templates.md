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
the sort of thing is a quiet word beside it: `OddSkilla  device`, `EchoBox  device`,
`Track 3  mixer`, `Transport`. Nobody has to learn the umbrella to read the page.

**A soundmachine and an effect are both devices, and a link says so.** The rack holds devices and
a device is one or the other; to a hardware knob they are one thing, a box with a face, an id and
a control you rested the pointer on. Which of the two it is decides where the link is looked for
when a message arrives, the machine the track plays or an effect on that track's chain, and
nothing else about it. So the word in a file is `device`. A template already on somebody's disc
says `machine` and is read as one. `effect` is still refused: in one of these files it has only
ever meant a plugin, and a plugin cannot be pointed at.

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

## The mixer is one target

A knob is pointed at the mixer, not at one strip of it. The desk in front of you has a fader for
every strip, so what you keep, hand on or lay down again is the whole layout: cut by strip it was
a card per fader saying the same three words with a number changed, and a file per fader that
nobody could use. The master goes in with them, being a strip of the same desk.

So the mixer is the one kind whose id is left out of `ILinkTargets.KeyOf`, and its card is headed
Mixer rather than with whichever strip came first. The strip is not lost: it is still what an
individual link names, and a mixer template writes it on each of its lines instead of once in the
target.

```json
{ "control": "Slider 1", "channel": 1, "cc": 0, "parameter": "level", "strip": "1" }
{ "control": "Knob 1",   "channel": 1, "cc": 16, "parameter": "pan",  "strip": "master" }
```

The word master or the track's number counting from one, which is what the screen says. A
template written before the strip moved onto the line named its one strip in the target, and is
still read that way, since a file on somebody's disc outlives a decision about how cards are cut.

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

## The Menu part on a machine's face

A machine's face can carry a **Menu**: `ElementKinds.Menu`, dropped onto the panel in the
designer like a Knob, placed where the person building the machine wants it, and carried in
`machine.json` with the rest of the face. It turns no parameter and never will. What is in it
comes from the host through `IPanelMenu`, exactly the way `Keys`, `Take`, `Preset` and `Zones`
are already filled.

It is deliberately not named after what it holds. What it holds is going to grow.

**Which options it drops down is chosen in the designer**, a tick apiece, and the ticks are built
from `MenuOptionWords.All` so an option added later turns up without that page being told.
Two today:

- `surfaces`: the control surfaces there is a template for on this machine, one line each.
- `learn`: start or stop learning, which is the same mode Ctrl+Shift+M turns over.

A Menu naming no options carries all of them, which is what one dropped on a panel and left alone
should do, and what every machine written before an option existed goes on doing when one is
added. `IMenuOptions` is that rule on its own, so it can be asked without a window: a machine
naming an option this build has never heard of carries the ones it does understand rather than
refusing the part, and a line belonging to no option is always carried.

The name badge went the same way and for the same reason. `ElementKinds.InstrumentName`
is a part now, placed by the machine, where it used to be drawn over every panel from code in a
corner this program chose. Two goes at moving it out of the Menu's way, beside it and then
centred, both looked like furniture shuffled around somebody else's design, which is what they
were. What it shows belongs to the song rather than to the machine, so it comes from the host
through `IInstrumentName`, exactly as the Menu's lines do.

**A corner of the machine, and not of the window around it.** That is the whole reason it had to
be a part. A button on the editor's card would be the host talking about the machine from outside
it, would exist only in the designer, and would be gone in the rack's window and in a track's
instrument window, which is where somebody actually sits with a machine and a controller.

**It is drawn over the panel rather than in it**, so where it is dropped in the tree makes no
difference and its corner is the whole of where it is. Laid out with the machine's own controls
it would take a row of the face and push everything else about, and dropped into a column it
would land wherever the drop happened rather than where a hand looks for it. Two corners, both at the top: the top
right by default, being where every program has ever put this button, and the top left for a
machine whose own artwork wants that side.

The two lower corners were offered for a while and had to go. A panel taller than the window it
is shown in scrolls, and the bottom of the panel is then below the fold, so the button was really
there with nobody able to see it. That is worth writing down because of how it presented: as a
machine whose change had not taken, fixed only by restarting. The registry was innocent, and
measured to be: removing a machine in SETTINGS and adding it back really does redraw the panel
from the new file within the session.

**One menu to a machine**, and it is the only part with a limit. A second one is either in the
same corner drawing over the first or in another corner offering the same lines twice, and both
are the kind of mistake you only notice with the designer shut. Adding one where there is one
already says so and names what to do instead, which is to pick the one that is there and choose a
corner for it; turning some other part into a menu is refused the same way, and the one that
exists may still be turned into something else and back.

What it drops down today, on a machine two desks are pointed at:

```
  nanoKONTROL2  ·  12 controls
  MiniLab 3  ·  8 controls
  Learn a control
```

**A template is the links themselves and not a file.** It is the card the MIDI CC page draws, cut
by `ILinkTargets`, so a machine nobody has pointed anything at lists no surfaces at all and one
with a nanoKONTROL2 pointed at it lists that. Picking a surface re-applies its template through
`ControlLink.Take`, which takes back anything pointed elsewhere on that machine since.

Hardware A and B against machines 1 and 2 is four templates, and there is no conflict between
them: a link records the controller it was learned on, so A and B both drive machine 1 and
neither displaces the other. That is also why picking a surface is usually a no-op, and why it is
worth having anyway: it is the repair when a knob has been moved.

**That paragraph was true of the design and false of the code.** A link is displaced by the same
physical control being pointed somewhere else, or by something else being pointed at the same
target, and the second test had no controller in it: pointing B at machine 1 deleted A's link on
whatever it landed on, as it was learned, with nothing said. Both halves of the rule are about
one controller now, which is `ControlLink.SameDesk`, and a link naming no controller is the
wildcard it reads as everywhere else and is displaced by any of them.

It cost twice, because a template here is the links themselves. The surfaces line lists what
survived, so somebody with two boxes on the desk lost half a template and then found the repair
was made out of the damage: four knobs learned on the Korg, two of them learned again on the
Arturia, and the Korg's card came back saying two controls. Reported as both halves of one
sentence, the CCs not being saved per hardware and the hamburger restoring only half the knobs.
`Tests/ControlDeskTests.cs` is the rule.

**The learn line is the keystroke and not a second way of doing it.** It turns over the same
`ControlLink.IsLinking` and says which way it is about to turn it, since the menu is read again
every time it opens and there is no other sign of the mode on a machine's face.

**What a machine offers is a list of lines and not a menu.** `PanelMenuItem` is that shape:
what it says, a tip, whether it is worth pressing, which option it belongs to, and what pressing
it does. Flat. The library turns those into menu items where the panel is drawn, because a panel
described in a file has no business naming a toolkit's types, and the side effect is that the
whole of what a machine offers can be put a question to without a window.

`Midi/ControlMenu.cs` is what fills the menu today. It keys by `ILinkTargets.KeyOf`, the same
rule the cards are cut by, and reaches the links through a question defaulting to
`ControlLink.Current`, which is the door the instrument panel already goes through to offer a
link at all. A question rather than the door itself, so that having no desk at all can be tested:
a static cannot be stood in front of.

**The mixer has the same button and it is not the same part.** It is drawn by this program rather
than described by anybody, so there is nothing to drop a part into: the button sits in the mixer
card's own header and always shows the two things, with no options to tick. Only the lines are
shared, through `IMenuLines`, which is the one place a line becomes something on a screen. A mixer
link is on a strip and the menu names no strip, since the whole desk is one thing to point a
controller at.

`Tests/SoundMachineMenuTests.cs` and `Tests/MenuOptionsTests.cs` are the two halves, and most of what
they ask is not the happy path: no machine, no desk, an id that differs by case, a plugin and a
mixer strip that are not this machine's templates, a link naming no controller, a line pressed
after its links were taken off, an options list that is empty, untidy, repeated, or names a word
this build has never heard of.

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

A lane names an insert on a track's chain, through the same `ControlKind.Plugin` and the same
`ControlTargets.OnPlugin`, which is why neither was removed with the rest. It is a different
thing from a link and wants different answers: a lane is this song saying what a parameter does
over these lines, so it belongs to the song, it is not a fact about your hardware, and it has no
business being a template. A track's own instrument is deliberately not searched there, although
it may well be a plugin, because a lane names something on the chain and a plugin playing the
track is not on its chain.

## Still open

**Templates are still not read on startup, and should not be.** Laying links down is a thing
that should happen because somebody asked rather than because a file appeared. What was missing
was the list, and the shelf is that list: the folder is walked when a machine's Links part is
worked, so applying one is picking a name rather than finding a path. What has no such list yet
is the mixer and the transport, which have no face of their own to carry a part.

**The MIDI CC page still opens a file box.** Its Export and Import ask for a path. Export is the
way a template leaves this computer, which does want a destination; Import is the one worth
looking at again.

**The mixer, the transport and our own effects have no face to carry a Menu.** The mixer and the
transport are drawn by this program rather than described by anybody, so there is nowhere to drop
one; an effect of our own will have a described face when there are effect engines to have one,
and then it gets the same part for the same reason.

**More options.** The part exists to be added to. Anything a machine's own face should be able to
offer, that the machine cannot know by itself, is a word in `MenuOptionWords` and a line from
whoever fills the menu.
