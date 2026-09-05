# EchoBox

A delay. What went past comes back later, darker each time. The plainest effect there is and the
first one this program shipped, and it goes on a track's chain, on the master, or on a pad.

## The controls

- **Time** is how long the repeat is, ten milliseconds to two seconds. Under about fifty it stops
  being an echo and starts being a tone, which is worth knowing rather than avoiding. The time
  glides rather than jumps: a delay line read from a different place on the next sample is a
  click, so every hardware delay either crossfades or slides, and this one slides. A time set
  before anything has been rendered is where it starts rather than somewhere to glide from, so a
  song opening does not smear its first repeats.
- **Feedback** is how much of the repeat goes back in, which is how many repeats there are, up to
  0.95. About 0.35 is three or four you can count; past 0.8 it is a wash that outlives the note
  that made it. It cannot reach one, deliberately: a delay that never decays is a delay that
  fills up.
- **Damp** takes the top off what comes out of the line, nought to one. Because that is also what
  goes back in, each pass round loses more of it, which is what makes a long feedback fall away
  into something soft rather than repeating a hi-hat forty times.
- **Mix** is how much of what you hear is the repeat rather than what went in. At one the dry
  signal is gone, which is what you want on a send and not what you want on a track.

## Working with it

Set the time first with the mix up so you can hear where the repeats land, then bring the mix
back down to where it sits under the part. On anything rhythmic, a time that is a division of the
tempo is the difference between an effect and a mess: at 120 beats a minute a beat is 500 ms, so
a quarter is 500, an eighth is 250 and a dotted eighth, which is the one everybody actually
wants, is 375. That is why the machine opens at 375.

A delay on the master carries the whole song into itself, which is almost never what you mean.
Put it on the track.

## What ships with it

Six presets: **Slapback**, one short repeat that thickens a voice, **Doubler**, shorter still,
**Quarter**, in time at a moderate tempo, **Tape Echo**, damped and fed back like a machine with
a worn head, **Dub**, long and dark with the feedback up, and **Wash**, which is mostly repeats.
