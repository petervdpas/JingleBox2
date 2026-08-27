# Hardware integration

Where a controller stands in relation to the program, worked out once, so that the fifth device
somebody plugs in is not a fifth special case.

Half of it is built. Each section says which half.

## The floor, and why it is not a fallback

A controller nobody has ever written a line about works today. Tick its boxes in SETTINGS, turn
on the other mouse mode with Ctrl+Shift+M, rest the pointer on a knob and touch the hardware.
`ControlSense` works out from three messages whether the thing you touched is a button, a fader,
a knob or an endless encoder, `ControlMapping` writes down a port name and a controller number,
and `MidiControlRouter` reconciles a hand that is somewhere with a parameter that is somewhere
else. Eleven links later there is a desk.

That path is the whole program, not a reduced version of it. Everything hardware can drive is
reachable this way, and it is the only path that will be true of every device anybody ever plugs
in.

So the rule this document exists to state:

```
a profile may add names, shape and shortcuts. it may never add capability.
```

The moment a feature works only when a profile exists, the feature is wrong and the profile is
covering a hole in the general path. There is a cheap way to keep that honest: the MPD218 in the
same room has no profile and is never going to get one. It is the test.

## The handshake, which does work

An earlier version of this document said there was no handshake, on the strength of a test done
here that got no reply. That was wrong, and the test was wrong. Corrected, with the wire to prove
it.

MIDI has a universal way to ask a device who it is. It is in the standard, it is six bytes, and a
MiniLab 3 answers it on its main port, immediately, in any program, repeatably:

```
sent      F0 7E 7F 06 01 F7

back      F0 7E 7F 06 02 00 20 6B 02 00 04 04 45 00 02 01 F7
                            |        |     |     |
                            |        |     |     `-- version 45 00 02 01
                            |        |     `-------- member  0x0404
                            |        `-------------- family  0x0002
                            `--------------------- Arturia, 00 20 6B
```

`sysex-controls` sends exactly those six bytes and parses exactly that reply, for Arturia and for
Korg both, which is what prompted the retest. The earlier negative result was a test failure, most
likely listening on the wrong port, and it stood in this document for a day as a design principle.

### Why that changes things

A profile can be matched on **identity** rather than on a port name. Which fixes the worst joint
in the whole design, the one written up below as a bug waiting to be filed as a mystery: a port is
called `Minilab3 MIDI` here and something with a leading digit on Windows, but

```
00 20 6B  0002  0404
```

is that string on every operating system, through every USB hub, with any number of the same
device plugged in. It is the one identifier that travels.

So the order of asking is:

```
1  the universal identity request      standard, answered by more than expected. try it first
2  the port's name                     always there. the fallback, and the match for devices
                                       that stay silent
3  the vendor's own protocol           Arturia, Akai and Korg answer much more than their name
```

And a device that answers none of them still works, because of the rule above.

Nobody picks a profile off a list. The match is automatic, and more to the point it is visible:
the row in SETTINGS says `MiniLab 3` where it used to say `Minilab3 MIDI`, so a wrong match is
something a person can see and correct rather than something that makes the device behave oddly
for a week. Hand-picking exists as that correction and never as a requirement.

### The lesson, which is not about MIDI

Two claims in this document were asserted from a single negative test: that identity does not
work, and that a device cannot be asked what its knobs send. Both were wrong, and both were only
found out because somebody else's source code was read. A negative result from one evening's
poking is a note to check again, never a foundation to build a design on.

## The rungs, in terms of what a person gets

```
nothing              works once you have taught it. A control reads "CC 89 ch 1".
a generic protocol   the device pretends to be a Mackie. Faders, pans, transport, track
                     select and a display, on a standard, without either side knowing
                     the other. See below: this is the rung this document first missed.
a profile            a control reads "Encoder 3". One row in SETTINGS, not two ports.
                     A layout that survives the device changing program.
the vendor's own     the screen and the lights, and the device's program read rather
                     than inferred.
```

Only the last holds anything genuinely unavailable without device knowledge, and those are the
things with no generic equivalent to degrade to: there is no plain-MIDI way to write text to a
screen. A device without a screen loses nothing by the application not writing to one.

## What the manufacturers actually do about a host they have never heard of

This document was written believing the answer was a file describing each device. It is not, and
the manuals say so in plain words.

The MiniLab 3, the small one:

```
1 DAW preset for automatic integration with any major DAW
DAW Transport Control with MCU protocol for every other DAW
```

The KeyLab MkII, the big one, has nine DAW presets. Six are named DAWs. The other three are:

```
Standard MCU     Mackie Control Universal
Standard HUI     the Pro Tools one
MMC              "for DAWs and MIDI Devices that do not support MCU/HUI"
```

with the manual's own advice for anybody not on the list: *"If your DAW isn't in the preset list,
it is probably compatible with either the MCU or HUI preset."*

So the industry's answer to the question this document exists to answer is not a description of
the device. It is **a generic control surface protocol**, of which there are three, and all three
are standards. The device stops being itself and pretends to be a Mackie Control, and any host
that speaks Mackie gets a working desk without either side knowing anything about the other.

That is a better rung than a profile, and it was missed here because the only device on the desk
sends four MCU notes and nothing else, so MCU looked like a way to read a transport row rather
than what it is.

### What that changes

Implementing MCU input properly is worth more than every profile this document proposed, because
it is one piece of work that lands on a whole class of hardware at once. Every KeyLab, every
X-Touch, every Icon, every Novation SL, every Behringer control surface, and the CMC and nanoKON
class of thing. All of them have a Mackie mode, because all of them had to solve this same
problem before us.

And what MCU carries is close to what the tracker already has:

```
faders          track levels, eight and a master
v-pots          pan, or a send, per track
buttons         solo, mute, record arm, per track
bank left/right eight tracks at a time
transport       play, stop, record, loop, rewind, forward
a display       the host writes track names to it
meters          the host writes levels back
```

`ControlKind.Mix` with `ControlScope.Fixed` was built for exactly this and before knowing it:
fader three is track three whether or not you are looking at it. The KeyLab manual describes its
own DAW mode in the same terms, independently: encoder N is the pan of track N in the selected
bank, fader N is the level of track N, fader 9 is the master.

The half to be careful about is that MCU is **bidirectional by design**. A surface expects to be
told the track names, the fader positions, the meter levels and which buttons are lit, and a host
that only listens gets a desk with dead lights. That is not a reason to defer it. It is a reason
to build the reading half first and know that the writing half is the rest of the job.

One caution before anybody writes code: everything in that table except the four transport notes
is from the protocol as it is commonly documented, not from this desk's wire. It needs an hour
with `aseqdump` and a KeyLab before any of it is trusted.

### And a smaller one, free

MMC is MIDI Machine Control: transport as universal system exclusive, in the MIDI standard
itself, and about twenty lines to read.

```
F0 7F <device> 06 01 F7    stop
F0 7F <device> 06 02 F7    play
F0 7F <device> 06 03 F7    deferred play
F0 7F <device> 06 06 F7    record strobe
F0 7F <device> 06 07 F7    record exit
```

It is transport and nothing else, so it is not an alternative to MCU. It is the thing to read
when a device offers it and nothing better, and it costs a switch statement.

## Five devices, and what each one asks for

The manuals in `docs/` are four controllers and a sequencer, which turns out to be a good spread
for testing whether the design holds.

```
MiniLab 3      8 encoders, 4 faders, 8 pads x 2 banks, a screen, 4 ports, 7 programs
KeyLab MkII    9 encoders, 9 faders, 16 pads, a screen, MCU/HUI/MMC, 9 DAW presets, 10 user
MPD218         6 knobs x 3 banks, 16 pads x 3 banks, no screen, one port, no DAW anything
KeyStep        a sequencer. clock master. not a control surface
KeyStep Pro    the same, four tracks of it
```

### The MPD218 is the honest case

Its manual contains no controller map at all. Not a table, not an appendix, not one CC number:
"visit akaipro.com and download the Preset Documentation". A profile for it would have to be
written off the wire, which is precisely what rung one does for free, by being touched.

Two things it confirms that had been inferred:

**Its knobs really are endless.** "6 360 degree assignable potentiometers." Which is the hardware
behind the first bug of this whole piece of work, the knob that flipped from its floor to its
ceiling: a pot that turns for ever but answers with a number that comes round.
`ControlPickup.Endless` was built for a device whose datasheet now says so in as many words.

**Its three banks are the same problem as the MiniLab's seven programs, and worse.** One button
changes what all six knobs send. No screen to say which bank, no DAW mode, no vendor protocol to
ask, nothing sent when it changes. On this device the program problem has no solution at all, at
any rung, and the right answer is the one already in place: a link is learned by touch and stays
where it was put.

### The KeyLab MkII is the case that pays

Nine faders and nine encoders, and a DAW mode that is a mixer. This is the device that makes MCU
worth implementing, and the device that shows what a default layout should be when there are
enough controls for one:

```
fader N     the level of track N in the selected bank
fader 9     the master
encoder N   the pan of track N
```

which is `ControlKind.Mix`, `ControlScope.Fixed`, `Track = N`. No new machinery, and it is what
the manual says the device already believes it is doing.

Also, Arturia uses one of our words and half of the other. Their faders in DAW mode offer "two
response behaviors: Jump or Pickup". This application calls those `Jump` and `Takeover`. Theirs
is the more common term and it would be worth matching, since it is what a person reading a
manual will be looking for in SETTINGS.

### The KeySteps are a different question entirely

Not control surfaces. Clock masters: they send MIDI clock over USB, and a KeyStep Pro will happily
run a tempo for everything else in the room.

JingleBox2 has no MIDI clock at all, in either direction. The tracker keeps its own time and
nothing outside it can start it, stop it or set its tempo. That is a real gap and it is not in any
plan document, this one included. It belongs in its own, because it is a timing problem rather
than a mapping problem and none of the rungs above apply to it.

## One device, measured, and then read up on

A MiniLab 3 in its DAW mode, every control worked once in physical order and read off the wire.
Then `docs/minilab-3_Manual_1_0_5_EN.pdf` and its cheat sheet, which settled several things the
wire could not and corrected two the wire got wrong.

### Its four ports, in the manufacturer's own words

The thing that looked like a device misbehaving is a device being deliberate:

```
Minilab3 MIDI       everything: notes, pads, encoders, faders, and the transport in DAW mode
Minilab3 DIN THRU   the host out to the 5-pin socket. an output. nothing arrives from it
Minilab3 MCU/HUI    Mackie Control on a port of its own, "to not interfere with other MIDI
                    messages as notes or control changes"
Minilab3 ALV        "transmits screen messages from Analog Lab V to MiniLab 3"
```

And the instruction that goes with them, which the application currently ignores: a host using
the DAW program is told to leave the MCU port switched off, because the custom mode and Mackie
Control would otherwise both answer the same press. So MCU and Controls are not two boxes to tick
on one device. They are a choice.

The last port is worth a test. The screen is written on the main port today and it works, but ALV
is the port Arturia says screen messages go down, and writing on it might leave the MCU port
undisturbed, which is the thing that broke shift+play the first time.

### Its programs, which is what changes every number

Not a "DAW mode" and an "ordinary mode". Seven of them, cycled by Shift and Pad 3, with the
current one on the screen:

```
ARTURIA        detects Analog Lab and maps everything to it
DAWs           the one this application wants
five more      user programs, made in Arturia's MIDI Control Center, each one enabled or
               disabled separately
```

So a layout is not hostage to a two-way switch that might be either way. It is hostage to one of
seven, five of which somebody made up. Any design that tries to know which mode a device is in by
recognising its numbers is guessing among an open set, which is the strongest argument yet for
asking the device rather than describing it.

### What each control sends, per program

DAW program, measured on the wire:

```
encoders   two rows of four, numbered along the top row and then along the bottom
             top     CC 86, 87, 89, 90
             bottom  CC 110, 111, 116, 117
sliders    CC 14, 15, 30, 31
transport  CC 105 loop, 106 stop, 107 play, 108 record, 109 tap tempo
pads       channel 10, bank A notes 36 to 43, bank B notes 44 to 51, Shift and Pad 2 swaps
keys       channel 1, and Shift with a key from F to G# picks the transmit channel
strips     pitch bend, and CC 1
```

ARTURIA program, from the manual, and this is the correction that matters:

```
knob 1  CC 74     knob 5  CC 93
knob 2  CC 71     knob 6  CC 18
knob 3  CC 76     knob 7  CC 19
knob 4  CC 77     knob 8  CC 16

fader 1 CC 82     fader 3 CC 85
fader 2 CC 83     fader 4 CC 17
```

Earlier this document listed that first set as "16, 18, 19, 71, 74, 76, 77, 93", which is the same
eight numbers sorted. They are not in that order on the front of the device. Sorted, they read
knob 8, 6, 7, 2, 1, 3, 4, 5.

There is a reason, and it generalises. A program aimed at a vendor's own instrument numbers its
knobs by **meaning**: 74 is filter cutoff and 71 is resonance in anybody's MIDI, so the first two
knobs get those numbers because of what they do. A program aimed at a DAW nobody has heard of has
no meanings to use and numbers **sequentially** instead. Which is why the DAW program ascends and
the ARTURIA one does not, and why the ordering heuristic below is safe in the only case it is
asked to work in.

### The transport, and the half hour it cost

The transport is not a row of buttons. It is pads 4 to 8 with Shift held, which is why a bare
press sends nothing at all on either port:

```
Shift + Pad 1   arpeggiator          Shift + Pad 5   stop
Shift + Pad 2   pad bank A/B         Shift + Pad 6   play
Shift + Pad 3   which program        Shift + Pad 7   record
Shift + Pad 4   loop on/off          Shift + Pad 8   tap tempo
```

Half an hour went on the port being open, the role ticked, the notes seen on the wire an hour
earlier, and play doing nothing. A profile should say which controls need a modifier. Not to send
one: so that a person setting the thing up is told, rather than left to conclude the software is
broken.

The manual also states the contract plainly, and it is exactly this document's rung one: **loop,
stop, play and record work in any DAW.** Everything past those four is a per-DAW script.

### What is sent back that we do not send

The device expects to be told things, and is currently told nothing:

```
pads 4 to 7 lit   amber loop, white stop, green play, red record, brighter when engaged
the screen        a play icon, a record icon, "Tap Tempo XX BPM" while tapping
```

Which is the same rung three as the screen text, and reachable the same way.

### One button with somewhere obvious to go

CC 109 is tap tempo, not "tab", which is what this document and the code both called it until the
cheat sheet said otherwise. Four taps is a tempo and the tracker has one. Nothing reads it yet.

## The device can be asked, which beats a file

`https://github.com/soyersoyer/sysex-controls`, GPL-3, a Linux replacement for the manufacturers'
own configuration software. It supports the MiniLab 3 and most of Arturia's range, along with
Akai and Korg, and what it does is read and write a device's own settings over system exclusive.

Arturia's protocol, from its `sc-midi.c`:

```
F0 00 20 6B 7F 42 02 <preset> <param> <control> <value> F7      write one setting
```

with a read counterpart that answers. Each knob on a MiniLab 3 has Output, Scale, Option,
Parameter MSB, Parameter LSB, Min and Max, and the eight of them are addressed at 0x0000 through
0x0700. Parameter MSB is the controller number it sends.

Which means the mode problem has a better answer than a file describing both dialects. The
application does not have to guess which mode is in use: **it can ask the device what each knob
currently sends**, and get the truth rather than a description. And it could set them, which
turns a layout from something the device decides into something the application does.

That reduces a profile to what it should have been. The names and the shape of the thing, "eight
encoders in two rows, four sliders, sixteen pads in two banks", with the numbers read from the
hardware at the time. A file saying what a MiniLab 3 *is*, and a conversation saying what it is
*doing*.

One correction it forces: the message this application sends before writing to the screen,
`F0 00 20 6B 7F 42 02 02 40 6A 21 F7`, is not a wake at all. It is one of these writes: preset
02, param 40, control 6A, value 21. Something is being switched on in the device rather than
roused, which is also the likeliest reason it stops speaking Mackie Control afterwards.

## The default layout

The piece worth building first, and the one that gives the most for the least.

A profile knows a device has eight encoders and what each is called. It does not know that
encoder 3 should be Zampler's cutoff, and it cannot, because that is a choice about machines the
profile has never heard of. So a profile on its own buys names, not links, and a device with a
profile still arrives blank.

What fills it without guessing is a layout expressed against the machine rather than against the
device:

```
the encoders take the machine's parameters, in panel order
the faders take the track's mixer strip
```

Panel order is already a real thing here: `MachinePanel.Root` is a tree, controls in it name
their parameter, and walking it depth first gives the order a person reads the face in. So the
third encoder drives the third knob on whatever machine is in front of you, on every machine,
including one somebody writes next year. Nothing is stored and nothing is guessed about the
hardware beyond what `ControlSense` already worked out.

The one thing it needs that sensing does not give is an **order**: which encoder is the first
one. A profile answers that exactly. Without a profile the answer is controller number ascending
within a kind, encoders ranked among encoders and faders among faders.

That is right in the case it is asked to work in and demonstrably wrong outside it. The MiniLab's
DAW program ascends left to right, and so does the MPD218. Its ARTURIA program does not, and is
not close: sorted, its eight knobs read 8, 6, 7, 2, 1, 3, 4, 5. The reason is above and it is a
reassuring one. A program written for a particular instrument numbers by meaning, and a program
written for a DAW nobody has heard of has no meanings available and numbers along the row. The
second kind is the only kind that will ever be pointed at this application.

That order shifts when a control nobody has touched yet turns up with a lower number. Which is
worth accepting rather than engineering around, because the default is a convenience for a device
you have not laid out. Any explicit link beats it, so a shifting default costs nothing that was
ever decided on purpose. The fix for finding it annoying is to lay the device out, or to write it
a profile.

In code this stays inside the existing pipeline rather than beside it. The default produces an
ordinary `ControlMapping`, which is never saved, so pickup, parking, sensing and the screen all
work on it unchanged. Two small additions:

```
ControlMapping.Ordinal      "the Nth parameter of the machine in front of you", when there
                            is no Key. Machine already means "any of them" when empty.
ControlTargets              resolves an ordinal by walking the panel tree, the same way it
                            resolves a key today.
```

One caution for whoever builds it. `MidiControlRouter` holds each mapping's hand state in a
`ConditionalWeakTable` keyed on the mapping object, so the default layout has to hand back the
same instance for the same control every time. Computing a fresh one per message would reset
pickup on every message and the knob would jump.

## The file, and who is expected to write one

This is the part that decides whether the application supports three controllers or fifty, and it
is not a technical question. Nobody here is going to own fifty controllers. Every device past the
ones on this desk arrives because somebody else owned one and could be bothered, and how bothered
they have to be is a design decision taken here, in advance, by whoever picks the format.

### What Reason does, and where it charges admission

Worth copying the shape and not the terms. Reason splits it in two:

```
a codec    .lua       what the surface physically has: keys, knobs, buttons, pedals,
                      meters, displays. written per device, once
a map      .remotemap tab separated text. which surface control drives which parameter
                      of which Reason device
```

That split is right and this document arrived at the same one from the other end. What a device
*is* and what a control *drives* are different facts with different lifetimes: the first is true
of every copy of that hardware for ever, the second is one person's desk on one afternoon.

The hoops are the rest of it. The codec is a Lua program, so contributing means programming. The
SDK is something you apply for and are granted, with a developer forum behind it. The map is tab
delimited, which is a format that punishes editing by hand. The result is visible from outside:
there are forum threads titled "The problem with Reason Remote", people publishing codecs for
single controllers on GitHub as projects in their own right, and at least one person who has
written a tool whose entire job is generating these files so a human does not have to.

That is the bar, and it is not a high one.

### What it should be here

```
a controller file     JSON. what the device has and how it says so. no scripting
the links             not a file at all. made by hovering a control and touching the
                      hardware, which is already built and already how it works
```

The second line is where most of Reason's difficulty goes. A codec needs to be a program partly
because an encoder has to be decoded, and every manufacturer decodes differently. Here that is
`ControlSense`, which works it out at runtime from three messages, so the file never has to
describe behaviour, only naming and shape. Declarative all the way down, and a person who cannot
program can still write one.

```
controllers/minilab3.json

  name        MiniLab 3
  identity    00 20 6B, family 0002, member 0404      matched first, on every platform
  matches     "Minilab3*"                             for devices that will not say
  vendor      arturia                                 which protocol to try for the screen
  ports       controls  "Minilab3 MIDI"
              transport "Minilab3 MCU/HUI", protocol mcu
  pads        channel 10, from note 36, two banks of eight
  strips      pitch bend; modulation on CC 1
  sliders     CC 14, 15, 30, 31
  encoders    eight, in reading order, and what each sends per program
                arturia   74, 71, 76, 77, 93, 18, 19, 16
                daw       86, 87, 89, 90, 110, 111, 116, 117
  needs shift transport
```

What it must not hold is what any control drives. That is the person's, or the default layout's,
and a file that carried it would be one manufacturer deciding somebody else's desk.

### The three rules that keep it honest

**No device name appears in the source.** If `minilab3` can be grepped out of any `.cs` file, the
design has failed and a contributor will have to send a patch rather than a file. This is the same
rule machines already live under, and it is checkable in one command.

**A file is dropped in a folder and works.** Shipped files are copied into the application folder
on first run and are a starting point rather than the answer, the way `MachineRegistry` does it.
No rebuild, no registration, no list of known devices to add a line to.

**A device with no file works anyway.** Contribution is optional for ever. That is rung one and
it is why nobody is ever blocked waiting for somebody else's hardware to be supported.

### And the thing that removes the hoop entirely

The application should write the file.

Everything a controller file holds is already discovered by the program in the course of ordinary
use. Link mode knows which control you just touched. `ControlSense` knows whether it is a button,
a fader, an endless encoder and which way it counts. The identity request knows the manufacturer,
family and model. The port list knows the ports. What is missing is a name for each control and
somewhere to put the result.

So: a page that says "touch each control in order, left to right", and at the end writes
`minilab3.json` into the application folder. The person who contributes device support does it by
turning their own knobs for two minutes and sending somebody a file they never opened.

That is a day of work, it makes the file format its own test, and it is the difference between a
format that describes fifty controllers and one that describes the three on this desk.
## Names are not the same on two platforms

The port name is the identity, and it is not the same string everywhere. Linux says
`Minilab3 MIDI`. Windows will say something closer to `2- MiniLab3`, with a leading number that
moves depending on what else is plugged in.

Two consequences, and the second is a real bug waiting to be filed as a mystery:

- A profile matches on a pattern, never a literal.
- Links already saved store the raw name they were learned on, so a layout made on Linux answers
  to nothing on Windows. Nothing warns, because a mapping whose device is absent is deliberately
  left alone. Worth knowing before somebody calls it a bug: it is not the link that moved, it is
  the port.

The fix is identity. `00 20 6B, family 0002, member 0404` is the same on every operating system,
so a profile matched that way covers both platforms without a pattern, and a link resolved through
a profile can name the profile rather than the port. Only a device that stays silent falls back to
matching on a name, and only a link with no profile falls back to storing one.

## What is built already

```
ControlSense            what kind of control this is, from three of its messages
ControlMapping          a link: device, channel, CC, and what it points at
MidiControlRouter       pickup, takeover, encoders both conventions, parking at the ends
ControlTargets          machine parameters, plugin parameters and mixer strips, uniformly
ControlLink             the two layers, the song's and the rack's
MidiTransportRouter     MCU notes on one port, Arturia's CCs on the other
ArturiaDisplay          the screen, in Arturia's own system exclusive
```

Built since: profiles and codecs, in `Controllers/`. A `.json` per controller naming its
controls and its ports, a `.lua` per controller translating what the application cannot read,
both optional, both matched on the port's name, both reloaded from the folder rather than
compiled in.

Still not built: the default layout, matching on identity rather than on a port name, reading
the device's settings back, and Mackie Control.

The transport half needed nothing from profiles, which is the rule above holding up in practice.
`MidiTransportRouter` reads whichever port is ticked for `MidiDeviceRole.Transport` and works the
same `TransportSwitch` the caps and the space bar use, so it is page sensitive without knowing it.
Cycle, rewind and forward are recognised and named in the log while doing nothing, so finding a
use for one is a line in a switch rather than a rediscovery of the protocol.

## The screen, and what does not work

Mackie Control can write text to a device's display, and a MiniLab 3 in DAW mode does not listen.
Tried on the MCU port, with nothing appearing on the screen and nothing coming back:

```
F0 00 00 66 14 12 00 <text> F7    Mackie Control display, first line
F0 00 00 66 10 12 00 <text> F7    the same as Logic Control, device id 0x10
F0 00 00 66 14 12 38 <text> F7    second line, offset 56
F0 00 00 66 14 00 F7              MCU device query, no answer
```

The universal identity request was listed here too, as unanswered. It is answered, on the main
port, and the entry was a bad test. See the handshake section above.

Not surprising in hindsight. MCU's display message was written for a two line character LCD; this
device has a colour screen, and Arturia drives it their own way. The port speaks MCU well enough
for buttons and is deaf to everything else.

## The screen, and what does work

Arturia's own, and built. `Midi/ArturiaDisplay.cs`.

From `https://gist.github.com/Janiczek/04a87c2534b9d1435a1d8159c742d260`, reverse engineered from
what Arturia's own software sends.

```
F0 00 20 6B 7F 42 02 02 40 6A 21 F7                              wake it, once per device
F0 00 20 6B 7F 42 04 02 60 01 <first> 00 02 <second> F7           two lines of words
F0 00 20 6B 7F 42 04 02 60 1F KK HH VV 00 00 01 <first> 00 02 <second> F7
                                                                  words, and a value drawn
  KK  03 knob, 04 fader, 05 pad
  HH  00 stay, 02 go back after a moment
  VV  0 to 127, drawn as the bar
```

The third is the one used: a knob being turned puts its name over its reading with the value drawn
beside them, and the screen goes back to whatever it was showing afterwards, because the reading
matters while a hand is on the knob and not once it has gone.

On the main port, not the MCU one. And the device has to be in a DAW mode: it ignored all of this
until the mode was switched, after which it took it immediately.

Two things about writing the text. Sixteen characters a line, and anything outside plain ASCII is
replaced rather than sent, because a byte above 127 ends a system exclusive message early and the
screen would show whatever fragment arrived before it.

Nothing asks whether a device has a screen. One without an output is answered by
`IMidiService.Send` with a quiet false, and a few bytes down a port nobody reads cost nothing, so
this needed no profile to tell Arturia's devices from anyone else's. That is the pattern for all
of rung three: attempt it blind and fail silently, which costs less than infrastructure to decide
whether to attempt it.

## What the wire cannot carry yet

Two gaps below all of this, found by reading `MidiService.Read` rather than by guessing.

**Pitch bend is dropped.** The parser handles note on, note off and control change, and returns
null for everything else. Mackie Control sends its nine faders as pitch bend, one per channel,
because pitch bend is the only 14-bit message in MIDI and a fader wants more than 128 positions.
So the largest single piece of hardware integration in this document is blocked by three lines in
a switch statement, and by `MidiMessage` having somewhere to put fourteen bits.

**System exclusive is dropped, on purpose.** A status byte of 0xF0 or above clears the running
status and returns null, which is right today: nothing reads one. Reassembly is perhaps thirty
lines whenever something does, and it belongs there, at the wire, beside the running status it
sits next to. It is not a module. See the note on that below.

**There is no module in this whose subject is system exclusive.** It is an envelope, not a job.
A device's screen, a transport command, a settings read and an identity reply have nothing in
common except a first byte, and grouping them by it would cut straight across the pattern the
`Midi/` folder already keeps: one router per role, each knowing a protocol and nothing about the
application. So reassembly goes in `MidiService`, MMC goes in `MidiTransportRouter`, Mackie
Control goes in a router of its own beside the other three, and Arturia's settings protocol goes
somewhere that is about Arturia.

## Order, and effort

```
1  the default layout           panel order walk                        an hour
                                an ordinal on a mapping, resolved       an hour
                                the live layout, cached per control     half a day
   no profile needed, and testable on the MPD218

2  Mackie Control, reading      pitch bend through the parser           an hour
                                faders, v-pots, buttons, banks          a day
                                verifying all of it on a wire           an evening
   one piece of work, and every control surface made in twenty years answers to it

3  profiles                     BUILT. the file, the registry, naming a control in both
                                lists, what each port is for in SETTINGS, and which program
                                the device is in worked out from what arrives
                                still open: one device row rather than four, and a link
                                that stores the control's name rather than its number

4  Mackie Control, writing      track names, meters, lit buttons        unknown
   the half that makes a surface feel connected rather than merely wired

5  the vendor's own protocol    reading a knob's current CC back        a day
                                pad colours, encoder rings              unknown

   MMC, whenever system exclusive is being read anyway                  an afternoon
```

The order matters more than the estimates. One is worth having on its own and needs nothing.
Two is the one that turns this from a MiniLab feature into hardware support. Three makes one and
two correct rather than lucky, and everything after that is comfort.

## Still open

- Whether the screen should be written on the ALV port rather than the main one. Arturia says that
  port exists for screen messages, and it may be why writing on the main port knocks the device out
  of Mackie Control. One evening with the device in DAW mode settles it.
- MCU and the DAW program are a choice and SETTINGS presents them as two independent tick boxes.
  Nothing warns that ticking both means every press is answered twice.
- Whether the transport port needs MCU implemented properly. Four notes is enough to start, and
  the rest of MCU is bidirectional: the host is expected to talk back, which is how the pads and
  the encoder rings light up. That is a bigger and more interesting piece and belongs in its own
  document.
- Tap tempo reaches the application and is thrown away. Four taps is a tempo.
- Loop reaches the application and is thrown away, because there is nothing to loop yet.
- What happens when a profile and the hardware disagree, which will happen the first time Arturia
  changes a firmware. The measured number should win over the file, and the file should be the
  thing that gets corrected.
- The main encoder is "varies depending on connected software", which means in the DAW program it
  is ours to define and nothing has been defined. It is the one control on the device with a
  screen attached to it.
- `ControlPickup.Takeover` is called Pickup by everybody else, Arturia's own manual included.
  Worth renaming what SETTINGS shows, if not the enum.
- **MIDI clock, which is a gap this document found and does not cover.** JingleBox2 keeps its own
  time and cannot be started, stopped or tempo-set from outside, nor drive anything else. A
  KeyStep or KeyStep Pro is a clock master sitting on the same USB. That is a timing problem
  rather than a mapping one and none of the rungs here apply to it, so it wants a document of its
  own before it wants any code.

## Sources

```
docs/minilab-3_Manual_1_0_0-cheatsheet_EN.pdf
                     one page, and the faster read of the two
docs/minilab-3_Manual_1_0_5_EN.pdf
                     the device, from its manufacturer
docs/keylab-mkii_Manual_2_2_0_EN.pdf
                     MCU, HUI and MMC, and a mixer's worth of controls.
                     section 4.9 is the one that changed this document
docs/MPD218-UserGuide-v1.0.pdf
                     20 pages, 5 languages, and no controller map at all
docs/KeyStep_Manual_1_1_1_EN.pdf
docs/keystep-pro_Manual_2_5_2_EN.pdf
                     clock masters, not control surfaces. a different question

https://github.com/soyersoyer/sysex-controls
                     a repository, GPL-3. the settings protocol, in src/sc-midi.c,
                     and the thing that proved the identity request does answer
https://gist.github.com/Janiczek/04a87c2534b9d1435a1d8159c742d260
                     a gist, one page. the display messages
https://www.arturia.com/products/hybrid-synths/minilab-3/overview
                     "1 DAW preset for any major DAW, MCU for every other DAW"
```
