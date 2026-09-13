# Track effects

The effects on the track the cursor is on, ours and the plugins alike.

The effects on the track the cursor is on, in the order the audio goes through them.
Moving the cursor to another track changes what this row is about.

When the track plays a plugin instrument, that plugin is the first block in the row,
because that is where it is in the audio: it makes the sound and everything after
works on what it made. Opening it gives you its own interface, and what you turn
there is what the pattern plays. Its sound is written into the song when you save.
Dragging an instrument from the song's list onto this row puts it on the track, the same
as dropping it on the track's column.

**The MIDI block in front of the chain is where the track's notes come from and go to.**
It has two rows, each a port and a channel.

- **In**: notes arriving on that channel play this track's instrument, wherever the
  cursor is, and are written into this track while record is armed: on the line that
  is playing while the transport runs, and on the cursor's line while it is stopped.
  Keys held together go into the track's columns side by side. *Any port* listens on
  every port that is open. A port named here is opened for the song even when it has
  no job in SETTINGS, and a note this track takes does not also go to the cursor's
  track. Off is no channel at all.
- **Out**: the notes this track plays, from the pattern and from its MIDI in, are sent
  to that port and channel. The track's own instrument still sounds, so a track with
  no instrument only sends. Every note still held is let go of when the transport
  stops.

Both are saved with the song, so each song can be wired its own way. A sequencer such
as a KeyStep Pro sends each of its tracks on a channel of its own, so four tracks here
can each listen to one of them.

The plus adds an effect to the end of the chain, and it offers two kinds. The
effects this application ships come first, since that list is short and known and a
plugin list runs to hundreds; ours load in this process with nothing to find on disc.
After them are the CLAP and VST3 effects this machine has.

Seven ship at the moment, each with presets to start from, and every one of them has a
**Level** knob last in line, which sets how loud the track comes back out of it:

- **EchoBox** is a delay. The repeats darken as they go, and the time glides rather
  than jumping, so moving it sounds like a tape slowing rather than like a click. Under
  Tape it bounces its repeats from side to side, wanders like a worn transport, and
  bends what goes back in so a long feedback holds instead of running away.
- **Sweeper** is a filter: four poles, low, band or high, with a drive in front of
  them. The drive is what makes a resonant sweep sound like an instrument instead of
  a whistle, and the cutoff glides for the same reason the delay's time does. Under
  Movement the cutoff can swing on its own or follow how loud the track is, which is
  an auto-wah.
- **Roaster** is a drive. The tilt chooses which end gets bitten, up for a desk and
  down for an amplifier; the bias leans the signal off centre, which is the half of a
  valve people actually like; and what the curve costs in level is given back, so you
  are comparing the sound and not the loudness. The curve is Warm, Hard or Fold,
  which are an amplifier, a fuzz and a wavefolder.
- **Shifter** moves the pitch without moving the speed: two taps read through a delay
  line at the wrong rate and crossfaded, in whole steps and in cents, with a window
  that trades the warble against the smearing. Under Double it pulls the two sides
  apart into a doubler, and feeds what it moved back in, which is shimmer.
- **Ringer** multiplies the signal by a tone, which is the sound of a machine talking.
  A sine or a square carrier, a spread between the two channels, and a crush on top,
  with a swing that moves the carrier up and down like a siren.
- **Widener** puts a mono source across the room instead of in the middle of your head:
  a width, a slow drift under it, and a Haas delay on whichever side you choose, with
  the bass kept in the middle below a frequency you set.
- **Phaser** sweeps notches up and down through the sound: four to twelve stages, a
  feedback that sharpens them without making anything louder, and a spread that moves
  them across the room.

A block opens that effect's controls in a window of its own, and its power button
switches it off without taking it out, so it can be heard in and out. Each block
prints its first few controls and what they read, so the row tells you the order and
the settings without opening anything.

**Drag a block along the row to change where it is in the chain.** A line down the
edge of a block says which side of it the one in your hand would go in at, and letting
go over the instrument at the head of the row means in front of the first effect. The
instrument itself does not move: it makes the sound and everything after works on what
it made, so first is the only place it has. Right click still has Move earlier and
Move later, a step at a time, and Remove.

An effect of ours carries a page about itself the way a machine does: the hamburger in the
corner of its face has **Help** on it where its author wrote one, and all six that ship
have one.

The mixer's master has a chain of its own, and so does a pad. The same effect on two
tracks is two sets of knob positions.

Chains are saved with the song. An effect that is missing when a song is opened is
named rather than passed over, and the rest of the chain still loads: for a plugin
that means one that is not installed here, and for one of ours a build that has no
engine for it.
