# Track effects

The chain of plugins on the track the cursor is on.

The effects on the track the cursor is on, in the order the audio goes through them.
Moving the cursor to another track changes what this row is about.

When the track plays a plugin instrument, that plugin is the first box in the row,
because that is where it is in the audio: it makes the sound and everything after
works on what it made. Opening it gives you its own interface, and what you turn
there is what the pattern plays. Its sound is written into the song when you save.

The plus adds an effect to the end of the chain, and it offers two kinds. The
effects this application ships come first, since that list is short and known and a
plugin list runs to hundreds; ours load in this process with nothing to find on disc.
After them are the CLAP and VST3 effects this machine has.

A box opens that effect's controls in a window of its own, and its power button
switches it off without taking it out, so it can be heard in and out. Right click a
box to move it earlier or later, or to remove it. Each box prints its first few
controls and what they read, so the row tells you the order and the settings without
opening anything.

The mixer's master has a chain of its own, and so does a pad. The same effect on two
tracks is two sets of knob positions.

Chains are saved with the song. An effect that is missing when a song is opened is
named rather than passed over, and the rest of the chain still loads: for a plugin
that means one that is not installed here, and for one of ours a build that has no
engine for it.
