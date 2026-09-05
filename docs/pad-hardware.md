# A pad box of our own

Thought about on 2026-09-05. **Nothing here is built.** This is the design, the machinery it
would need, and what was considered instead.

## What it is

A box of buttons on a microcontroller, plugged into the host's USB, speaking a protocol of this
application's own over a serial line. Not a keyboard and not a MIDI device: **a peripheral this
application owns**, the way the mixer is a page this program draws rather than one anybody
described to it.

The wire carries more than a press. The host says how many pads there are, how they are arranged,
what each is called, what colour it is and whether it is playing. The box says which button went
down and which came up.

## Why not MIDI, having spent a page arguing for it

Because of what has to travel. A note is seven bits and a channel, and everything above that has
to be system exclusive, which is a private protocol with worse framing and a manufacturer id
nobody owns. **If the vocabulary is going to be ours either way, it may as well be one you can
read with `cat /dev/ttyACM0`.**

That is not the main reason though. The main reason is this:

**The host says what is true and the box decides what to do about it.**

The host says there are eight pads in four rows of two, that pad 6 is called Intro Bed, that it
is orange, and that it is playing. A sixteen button box lights eight and leaves eight dark. A
thirty two button box does the same. A box with a screen on it draws the names. A box with one
row of four banks through them. **None of that is this application's problem**, and that is what
makes the constraint this whole idea started with go away: the ceiling moves to the device, and
the device is allowed to disagree with it.

Over MIDI that conversation cannot be had. The box would be told a note fired and nothing else,
so the box would have to be configured to match the matrix by hand, which is the coupling being
removed.

## The objection, which is real

**It is a second way of firing a pad**, standing beside the link layer, and two ways of doing one
thing that answer differently is the fault this codebase has already paid for in `MidiRouter`, in
`ControlScope.Focused` on the mixer, and in the two layers templates used to live in.

The answer is that a link exists because **somebody else's hardware does not know what it is
for**. A nanoKONTROL2's CC 22 means nothing until it is pointed at something, which is the whole
reason learning exists. A box built for this application knows: its button 7 is pad 7 by
construction. There is nothing to learn, nothing to displace, nothing to export, and no card to
head. It is not a second mapping layer. It is a device.

The line that keeps that honest: **the day it grows a Learn step it has become the link layer and
should be the link layer.**

## The protocol

Lines of ASCII, newline terminated, words rather than numbers, which is the rule a `.jbtl`
already keeps and for the same reason: it can be read, corrected and sent on by somebody who has
never seen this code, and it can be driven by hand from a terminal while the firmware is being
written.

The box says:

```
jbpad 1 16          protocol 1, and I have sixteen buttons
fire 7              button 7 went down
up 7                button 7 came up
```

The host says:

```
pads 8 4 2          eight pads, four rows, two columns
name 6 Intro Bed
colour 6 FF8800
state 6 playing     one of stopped, playing, error
done                that is all of it
```

**A hello is answered with the whole state and never with a difference.** A box that missed one
line is a box showing the wrong label, and for sixteen pads the whole of it is a few hundred
bytes, so there is no cheaper way to be certain than saying all of it. After that, one line per
change, each compared against what was last sent and dropped if it would say the same thing
again, which is `MackieSurface`'s rule and is there for the same reason: a name is fifty bytes
and the pads move for all sorts of reasons.

**Both halves of a press, and the box says nothing about what they mean.** Whether a pad toggles
or plays while held is the pad's own setting, and a box that decided it locally would be a second
opinion about a thing the application already knows. This is the rule `IMidiMonitor` already
keeps: a key is an event with two halves and a sound is a thing with a length.

**Which port is ours is asked, not configured.** Walk the serial ports, write `hello`, and
whatever answers `jbpad` is the one. Not a hardcoded vendor and product id, since a Pico, a
Leonardo and a Teensy have three different ones and which chip it is built on is not a fact worth
writing down. A port that does not answer within a moment is left alone, which matters, because
the ports on a machine also include somebody's modem.

**Nothing in the protocol says serial.** That is deliberate and it is the one piece of foresight
worth paying for here: the same lines over TCP are a phone or a tablet on the wifi drawing a pad
wall with real names on it, which is the touchscreen idea without a Pi, without a second copy of
the application, and without the work of keeping two of them in step. Not planned. Just not
precluded.

## What this application would need

Three pieces, in the shapes this codebase already uses for exactly this. `Log` is the precedent:
a door that cannot be handed about, with everything it knows taken out into contracts that can be
asked without a process.

`IPadLine` is the shape of a line, read and written, **testable with no port and no hardware**,
which is where every unhappy case goes: a line cut in half, a name with a newline in it, a pad
number off the end, a word from a later version of the protocol.

`IPadPort` is the connection. The walk that finds it, reading lines off it, writing lines to it,
and going away without taking anything with it.

`PadBox` is the door. It listens to `IAudioEngine.PadPlaybackChanged`, which already carries
`(PadIndex, State, Message)` with exactly the three states the protocol needs, holds what it last
sent, and hands presses on.

**A press goes through `IControlWrites.Pressed` and this is not optional.** That interface takes
no mapping, so a serial box can use it as it stands, and it is the queue that exists because two
pad hits in the same millisecond became one toggle on a real device. A new path posting straight
to the drawing thread would put that fault back, in a place nobody would think to look for it.

**Unplugging may not block anything.** `docs/threads.md`'s rule holds here: the loser refuses
rather than waits. A write to a port that has gone fails quietly and closes the box, the reader
is a thread of its own, and its death is not an application event. A pad box pulled out mid show
must cost nothing but the pad box.

`System.IO.Ports` is a package this project does not reference yet, and **its behaviour on Linux
when a device is removed is the thing to check before committing to any of this**, since the
failure it is known for is a hang, and a hang here is the drawing thread.

## The box

The protocol makes the hardware nearly a free choice, which is the point of it. What is worth
saying anyway:

Serial is the one thing a classic Arduino is unambiguously good at, so **the choice of Arduino
stops being a compromise the moment the protocol stops being MIDI.** A Uno cannot do
class compliant USB MIDI without reflashing its USB chip; it does CDC serial out of the box, and
so does everything newer.

Buttons as a scanned matrix with a 1N4148 per button. The diodes are not optional: firing three
pads at once is the ordinary case here rather than a stress test, and a scanned matrix without
them reports ghosts.

Translucent 30mm arcade buttons, about a euro and a half each, with a paper insert under the cap.
Cheap, built for millions of presses, and the label problem solved for the price of a screen's
bezel. `colour` and `name` on the wire only start earning their place once the box has RGB or a
display, which is a decision about the box rather than about the protocol, and the protocol
should carry them regardless so that the box can be improved without the host being touched.

Velocity buys nothing. A pad's level is the pad's own setting, so force sensors are cost and
firmware for a number nothing reads.

## What was considered instead

**MIDI notes through pad links.** Works today with no application code at all: Ctrl+Shift+M, rest
on a pad, hit the hardware. Carries no names, needs a learning step, and shows nothing back. Its
real value is not as a rival design but as the way to find out whether a pad box is wanted at
all, this afternoon, with any pad box already in the house.

**A USB numeric keypad.** Eight euros and no build, and the digit block is a 3x3 that maps onto a
nine pad wall one key per pad. It fires only while FIRE has focus, unless the device is grabbed at
the evdev layer, which is a platform branch. Typed codes are the cart machine and every playout
system since, and they win where a library outgrows the buttons, which is a different problem
from this one and does not need hardware at all.

**A QMK macropad**, about thirty euros, real class compliant MIDI with per key RGB and no
soldering. Beaten here only by the vocabulary: it can be pointed at pads and it cannot be told
what they are called.

**A Pi with a touchscreen.** The only shape with no ceiling. If it happens, run the application
rather than building a device with a screen: `linux-arm64` is already a published runtime
identifier with BASS beside it. It boots for twenty seconds and it can hang, which for live radio
is the whole argument against it.

## What is still open

Whether the box has a display or RGB at all, which is what decides whether `name` and `colour`
are carrying anything.

How a box with fewer buttons than pads behaves. It is the device's business by design, which is
the point, but somebody still has to decide it once and write it in the firmware.

Whether the same lines over TCP are worth doing, and whether that is better than the Pi running a
second head.

Whether `PadBox` should be told the pads by `MainViewModel` or read them, which is the ordinary
question of who owns the list, and is worth answering before anything is written rather than
after.
