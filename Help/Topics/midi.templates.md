# Control templates

What your hardware is pointed at, one card per controller.

MIDI CC is the list of every link you have made: one card for each controller against
each thing it drives, headed with the thing itself. Folded away to begin with and one
open at a time, because a card is ten or twenty rows and a desk pointed at six
machines is a page nobody can hold in their eye.

A card **is** the template. There is no separate file to keep in step: what you see is
what is in force.

## Making one

Rest the pointer on the control you want driven, hold `Ctrl+Shift+M`, and touch the
knob or button on the desk. It works on a machine's face, on an effect's, on a mixer
strip, on the transport, and on a pad on FIRE.

A link names the machine and the control, never the track or the song, so one knob is
OddSkilla's filter on every track and in every song. A mixer link names the strip,
and strip three is strip three everywhere.

It also remembers the controller it was learned on, because a CC number means nothing
on its own: two desks both have a CC 22. So hardware A and B pointed at machines 1
and 2 is four templates rather than a fight, and a controller you have simply
unplugged keeps its links, since leaving one in the other room is not a decision to
unwire it.

A link is displaced by two things and only two: the same physical control pointed
somewhere else, or something else on the same desk pointed at the same target.

## Handing one to somebody

**Export** on the card writes a `.jbtl` file. It is JSON, and every value in it is a
word rather than a number, so it can be read and corrected by somebody who has never
seen this program.

The port is the only thing in a link that cannot travel, since the same desk is
spelled differently by different systems, so a file names the controller as its
profile calls it and the ports are looked through on arrival. A controller that is
not plugged in keeps the name the file carried and its links wait for it, and the
page says so, because a template that applies perfectly and moves nothing until the
device arrives reads exactly like a file that failed to open.

Importing lays the links down by the same rules a link made by hand keeps, so
importing the same template twice leaves what it did the first time. What cannot be
read is counted and left out rather than failing the lot, which is what a template
from a newer version looks like.

## Where else it shows

A machine's own face can carry a menu, dropped there by whoever built it, listing the
control surfaces there is a template for on that machine: picking one re-applies it.
The mixer has the same button in its header, and so does FIRE for the pads.
