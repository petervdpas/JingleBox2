# Songs

What a song holds, where it is kept, and how one travels.

A song is a `.jibx`, which is a zip: the patterns, the order, the mix and a copy of
every instrument it uses, plus each plugin's own patch as that plugin handed it over.

The patches are kept beside the document rather than in it because they are almost
all of it, and because a document is all or nothing: one song here is 348 KB of which
the music is 781 bytes and one synth's patch is 331 KB, so a patch that comes back
damaged costs that patch and never the song.

## Saving

**Save song** writes it where it lives. **Save as...** writes it somewhere else and
works on that one from then on. **Cancel changes** reads the song back off disc as it
was last saved, and asks first.

Both buttons colour when there is something on screen that is not on disc: green on
the safe one and warm on the other, since the moment saving starts asking to be
pressed is the moment discarding starts being able to cost you an afternoon.

Cancelling does not stop the transport. It is an undo taken all the way back to the
file rather than a different song being opened, so a song that was playing goes on
playing, from the line it had reached, on whatever came back off disc. What it does
throw away is what you have undone and redone: the history goes with the changes.

## Recordings a song uses

A recording that lives in the application folder is written down by name rather than
by path, so a song survives that folder moving or being opened on another machine.

**Pack...** writes the same `.jibx` with the recordings inside it, wherever you
choose and never into the songs folder. Saving does not do this, because a song built
on a long take is tens of megabytes and the open song is written out every twenty
seconds.

What travels is decided per recording: a machine's own presets ship with the program
and are named rather than carried, and your own takes are carried. Opening a packed
song puts what it carried on the shelf and repoints the instruments, skipping
anything already there byte for byte, so opening one twice adds nothing.

**Import...**, on the Open song dialog, is how one arrives. It takes a `.jibx` from
anywhere on the machine and copies it into your songs, under a free name if that one
is taken, so nothing you already have is overwritten. There is no unpack of its own:
whatever the file carries arrives when you open it, which is the same press every
other song in the list takes.

**What a pack does not carry is somebody else's plugin.** A VST3 or a CLAP is another
program and has to be installed on the machine you are carrying the song to. What the
song does hold is that plugin's own patch and where its knobs stood, and a plugin is
looked for by its own identity before its path, so a bundle living somewhere else on
the other machine is still found. A plugin that is not installed there is named rather
than passed over, and the rest of the song still plays.

## Machines a song needs

A song carries its instruments but not the machines they are on. One that is not
registered here makes no sound and has no panel, the status line names it as the song
opens, and opening that instrument says so rather than showing an empty frame.
Adding the machine is SETTINGS, Devices.

## Repeating a slot

A slot of the order can play its pattern more than once, and can go round stretches
of its lines a number of times on each pass, which is the old trackers' pattern loop
set where you can see it. Both belong to the slot rather than the pattern, so a
pattern in the order twice can repeat in one place and not the other.

**Repeat the pattern**, on the slot's right click menu in the order list, sets how many
times the slot plays before the song moves on. **Loop the selected lines**, on the
pattern's right click menu, makes the lines you have selected go round that many times
on each pass and then carry on. A slot can have several of these, as long as no two
share a line: a loop is the whole row of lines whichever track you selected it on, so
lines that are already in a loop are refused, and the status line says which loop is in
the way. Selecting the same lines again changes how many times they go round, and
**Unloop the selected lines** takes off the loops the selection touches. The row in the
order list shows everything, for example `×2 ↺16–23×2 ↺48–63×4`, and
**Clear the repeats** puts the slot back to playing once.

Playing one pattern round, the lines still loop and the pattern simply goes round as it
always does. Coming round to the same slot again, by the song looping, repeats it again.
**Play it again** is different: it adds slots of their own after this one, so a run
reads down the list.

## Ending a slot early

A slot can stop before the end of its pattern and go on to the next slot from there,
which is the old trackers' pattern break: an ending, or a short bar in the middle of a
song, without making a pattern for it. Put the cursor on the last line the slot should
play and choose **End the slot after this line** on the pattern's right click menu. The
slot's row in the order list shows it as `to 23`, with a red cross that lets the slot
play the whole pattern again.

It belongs to the slot, like the loops, so the same pattern can play whole in one slot
and stop short in another. A repeat count goes round the shortened slot, and loops of
lines past the end are taken off since they could never come round. Playing one pattern
round, the whole pattern plays.
