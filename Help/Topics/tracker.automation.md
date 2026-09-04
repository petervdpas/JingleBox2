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

Recording one is playing it: move the control while the transport runs and the pass
leaves one undo step for the lane rather than one per point, which is the same rule
the instrument knobs use.

A lane is part of the pattern, so undo puts the notes and the movement back together
rather than putting the notes back and leaving the movement where it was.
