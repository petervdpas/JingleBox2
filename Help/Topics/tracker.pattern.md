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
