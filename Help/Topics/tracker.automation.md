# Automation

A control moving over the lines of a pattern.

The handle under the chain folds open a lane editor for the track the cursor is on.
Add a lane, pick which control it is about, and the curve is drawn in the room to the
right of it.

Time runs left to right although the pattern runs downwards, which is what a shape a
hand recognises does. Click to add a point or take hold of one, drag to move it,
right click to take it away. One gesture is one undo step.

Time snaps to lines, since there is no finer grid. A point dragged onto a line that
is already taken keeps its old time and moves only its value: a lane holds one point
per line, and a drag that ate its neighbours would destroy work on the way past.

The shape rests on the parameter's own nought, worked out from what it is: the floor
for a level, the middle for a pan or a pitch, since a pan drawn as a level reads as
hard left the whole way with a bump in it.

A lane names a strip rather than a track, so the master is automated exactly as a
track is. Its lanes are on the mixer, under the master, because the panel under the
pattern follows the cursor and the master is not somewhere a cursor can be.

The song's tempo is a lane on the master too, called Tempo. Draw a step and the song
changes speed on that line; draw a slope and it speeds up or slows down gradually. It
changes how fast the song plays, and the MIDI clock sent to other gear and the tempo
the plugins are told follow it; the tempo saved with the song stays as it is. While
the song plays, the BPM box above the pattern shows the tempo it is playing at, in
colour when a lane has moved it, and goes back to the song's own when it stops.

Lanes play whichever page is in front. A knob in your hand only reaches the mixer while
the mixer is showing, so one turn cannot move two things at once, but a lane is the song
itself and plays the same wherever you are looking.

Recording one is playing it: move the control while the transport runs and the pass
leaves one undo step for the lane rather than one per point, which is the same rule
the instrument knobs use.

A lane is part of the pattern, so undo puts the notes and the movement back together
rather than putting the notes back and leaving the movement where it was.
