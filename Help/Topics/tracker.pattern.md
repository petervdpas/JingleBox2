# The pattern

Where the notes are written, and how the grid is read.

A pattern is lines down and tracks across. The cursor stays on the middle of the
screen and the pattern runs under it, which is what makes the line you are working on
somewhere your eye can rest rather than a highlight to follow down the page. That
holds at the ends too: line 00 sits on the middle exactly as any other line does, and
what is above it is blank.

The pattern that is really coming next is drawn faintly in that space, so you can see
what you are writing towards. Only one that is really coming: in song mode it is the
next slot in the order, there is nothing at the two ends because a song does not
wrap, and there is nothing at all in pattern mode, where the only thing coming is
this pattern again.

## A cell

Each cell holds a note, an instrument, a volume and an effect. A blank instrument
means whatever this voice last played, and a blank volume the same.

The volume column runs 00 to 80, which is 128 steps, so a velocity from a keyboard is
written in unchanged and can be read back against what the keyboard said it sent.
Full is 80 and a key at its hardest is 7F.

## Commands

The last field of a cell is a command: a letter and two hex digits, such as `Q06`. It
acts on the note in its own cell, on that line, so every note of a chord can carry a
different one. On a line with no note it acts on the note still ringing in that
column.

The cursor stops on the letter and on the digits separately. On the letter any key
names the command, and the cursor moves on to the digits by itself; type two digits
and it steps down. Delete on either stop clears the command.

Every line is twelve ticks, so a delay of `06` is half a line and `03` a quarter.

The same commands can be picked instead of typed: right click a cell and choose
Command. Pick the command by name, then click a tick on the drawn line for a delay or
a cut, set how often and how much quieter for a retrigger, or pick a chord for an
arpeggio. The window says in words what it will do and which letters it writes. With
lines selected it writes all of them, which is how an arpeggio lasts several lines.

- `Vxx` sets the note's volume, `00` to `80`, and wins over the volume field.
- `Pxx` pans the note: `00` hard left, `40` centre, `80` hard right.
- `Qxx` starts the note `xx` ticks into its line, for swing, flams and pushed beats.
  A delayed OFF lets go late.
- `Cxx` cuts the note dead `xx` ticks into its line, with no release, for stabs and
  gated hats.
- `Rxy` plays the note again every `y` ticks. `x` makes each repeat quieter: `0`
  keeps the level, `8` halves it each time. `R04` is three hits in the line.
- `Axy` steps the note through itself, `x` semitones up and `y` up, one step a tick.
  `A47` is a major chord out of one voice, and `A37` a minor one. The next line puts
  the note back unless it carries on.

On our own machines an arpeggio moves the pitch of the note that is already sounding.
A plugin and a track's MIDI out are sent each step as a note of its own instead, and
a cut there is a note off.

The mixer's fader and pan are for a whole track; the volume field, `V` and `P` are
for one note.

## Note columns

A track is as many voices as it has note columns, one by default and up to eight. A
note played while another key is still held goes to the next column of the same
track, so a chord lands across one line, and the track widens itself to fit rather
than making you find a menu first.

A chord is written in pitch order rather than in the order your fingers landed, since
a column is a voice: E G B is written the same way every time it is played, and a
voice does not leap about inside its own column between chords.

Clearing a track gives back the columns it grew, by what the whole song uses rather
than what the pattern in front does: a track may not lose room another pattern's
chords are still in.

## What a new note does to the one before it

Cut, release or sustain, and it is the instrument's to say rather than the track's,
because it is a fact about the sound: a piano overlaps and a bass does not. Cut is
what a tracker has always done and is what everything starts on. A kit answers the
same question with its choke groups and is left out of it, so that a crash rings
under the snare that follows it.

## The order

The list down the left is the order the patterns play in. A slot can be dragged to
move it, a pattern can be copied, and the strip down the left of the list marks a
loop range by dragging: click a single slot twice to take one off again.

A range goes round whatever the loop switch says, and it is answered only at its last
slot, so playing from before it runs in and then loops, and playing from after it is
not dragged backwards.
