# Track effects

The chain of plugins on the track the cursor is on.

The effects on the track the cursor is on, in the order the audio goes through them.
Moving the cursor to another track changes what this row is about.

When the track plays a plugin instrument, that plugin is the first box in the row,
because that is where it is in the audio: it makes the sound and everything after
works on what it made. Opening it gives you its own interface, and what you turn
there is what the pattern plays. Its sound is written into the song when you save.

The plus adds an effect to the end of the chain. A box opens that plugin's controls
in a window of its own, and its power button switches it off without taking it out,
so it can be heard in and out. Right click a box to move it earlier or later, or to
remove it.

Chains are saved with the song. A plugin that is missing when a song is opened is
named rather than passed over, and the rest of the chain still loads.
