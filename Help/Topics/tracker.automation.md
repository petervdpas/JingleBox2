# Automation

A control moving over the lines of a pattern.

The handle under the chain folds open a lane editor for the track the cursor is on.
Add a lane, pick which control it is about, and the curve is drawn in the room to the
right of it.

The list offers what belongs to the pattern, in the order it is read: the instrument
first, a machine of ours or a plugin, then the effects on the track's chain. A plugin
instrument's controls appear once the plugin has started, which takes a moment after a
song is opened. The mixer's controls are not here: they belong to the whole song and
are automated on the mixer, under the strips.

Time runs left to right although the pattern runs downwards, which is what a shape a
hand recognises does. Click to add a point or take hold of one, drag to move it,
right click to take it away. One gesture is one undo step.

The picture is marked with round values in the parameter's own words, 50 BPM, 100 BPM
and so on for a tempo, and every point says its value while there are few enough to
read. A point snaps to a marked line when it is let go close to one, and otherwise to
a round number, so a tempo lands on whole beats a minute. Hold Shift to place it
freely.

Time snaps to lines, since there is no finer grid. A point dragged onto a line that
is already taken keeps its old time and moves only its value: a lane holds one point
per line, and a drag that ate its neighbours would destroy work on the way past.

The shape rests on the parameter's own nought, worked out from what it is: the floor
for a level, the middle for a pan or a pitch, since a pan drawn as a level reads as
hard left the whole way with a bump in it.

Where a lane lives depends on what it moves. Anything on the mixer, a track's level,
pan, mute, solo and ducking, the master's, and the tempo, belongs to the whole song: its
lane runs along the order, slot after slot, so a track can start soft, come up in the
middle and go down again at the end, and the picture shows the whole song with each
slot's start marked and numbered. Anything on a machine, an effect or a plugin belongs to
the pattern: its lane is written against the pattern's lines and comes along wherever
that pattern plays, so a sweep in a chorus plays every time the chorus does, and copying
a pattern copies its sweeps.

A lane names a strip rather than a track, so the master is automated exactly as a
track is. Its lanes are on the mixer with every other song lane, its effects included,
because the panel under the pattern follows the cursor and the master is not somewhere
a cursor can be.

The song's tempo is one of the master's lanes, called Tempo, and like the rest of the
mixer it runs along the whole song. Draw a step and
the song changes speed there; draw a slope and it speeds up or slows down gradually. It
changes how fast the song plays, and the MIDI clock sent to other gear and the tempo the
plugins are told follow it; the tempo saved with the song stays as it is. While the song
plays, the BPM box above the pattern shows the tempo it is playing at, in colour when the
lane has moved it, and goes back to the song's own when it stops. Typing `T` and two
digits into a cell writes a step into this lane.

Lanes play whichever page is in front. A knob in your hand only reaches the mixer while
the mixer is showing, so one turn cannot move two things at once, but a lane is the song
itself and plays the same wherever you are looking.

Recording one is playing it: move the control while the transport runs and the pass
leaves one undo step for the lane rather than one per point, which is the same rule
the instrument knobs use.

A lane is part of the pattern, so undo puts the notes and the movement back together
rather than putting the notes back and leaving the movement where it was.

Pattern automation in SETTINGS, Tracker switches this strip off on a machine with little
to spare. The lanes are kept in the song but are neither played nor recorded until it is
switched on again; the song's own lanes on the mixer have a switch of their own under
SETTINGS, Mixer.
