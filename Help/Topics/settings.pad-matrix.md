# Pad matrix size

How many pads, and in what shape.

Rows and columns for the pad grid. Minimum 4 pads (2x2 or 1x4), maximum 16 (4x4 or
2x8).

"Use extended pad matrix" raises that to 32, for a screen with the room for them.
It is a switch of its own because a grid of 32 is a different instrument from a grid
of 8, and not somewhere to arrive by holding an arrow key down. Turning it off again
leaves a big grid that is already in force alone; it only refuses the next one.

Changing the matrix stops all playing audio and rebuilds the grid. Pad settings are
kept where possible: a pad that still exists after the change keeps its sound, its
colour and its volume.
