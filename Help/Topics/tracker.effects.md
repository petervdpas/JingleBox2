# Track effects

The effects on the track the cursor is on, ours and the plugins alike.

The effects on the track the cursor is on, in the order the audio goes through them.
Moving the cursor to another track changes what this row is about.

When the track plays a plugin instrument, that plugin is the first block in the row,
because that is where it is in the audio: it makes the sound and everything after
works on what it made. Opening it gives you its own interface, and what you turn
there is what the pattern plays. Its sound is written into the song when you save.

The plus adds an effect to the end of the chain, and it offers two kinds. The
effects this application ships come first, since that list is short and known and a
plugin list runs to hundreds; ours load in this process with nothing to find on disc.
After them are the CLAP and VST3 effects this machine has.

Three ship at the moment, each with presets to start from:

- **EchoBox** is a delay. The repeats darken as they go, and the time glides rather
  than jumping, so moving it sounds like a tape slowing rather than like a click.
- **Sweeper** is a filter: four poles, low, band or high, with a drive in front of
  them. The drive is what makes a resonant sweep sound like an instrument instead of
  a whistle, and the cutoff glides for the same reason the delay's time does.
- **Roaster** is a drive. The tilt chooses which end gets bitten, up for a desk and
  down for an amplifier; the bias leans the signal off centre, which is the half of a
  valve people actually like; and what the curve costs in level is given back, so you
  are comparing the sound and not the loudness.

A block opens that effect's controls in a window of its own, and its power button
switches it off without taking it out, so it can be heard in and out. Right click a
block to move it earlier or later, or to remove it. Each block prints its first few
controls and what they read, so the row tells you the order and the settings without
opening anything.

An effect of ours carries a page about itself the way a machine does: the hamburger in the
corner of its face has **Help** on it where its author wrote one, and the three that ship all
have one.

The mixer's master has a chain of its own, and so does a pad. The same effect on two
tracks is two sets of knob positions.

Chains are saved with the song. An effect that is missing when a song is opened is
named rather than passed over, and the rest of the chain still loads: for a plugin
that means one that is not installed here, and for one of ours a build that has no
engine for it.
