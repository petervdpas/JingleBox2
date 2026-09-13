# Shifter

Pitch: what went past comes back at another pitch and at the same speed. Two taps run through a
delay line faster or slower than it is being written, and each is faded away before it runs out.

## The controls

- **Steps** is the interval, two octaves either way. Twelve is an octave. Nought is straight
  through, deliberately: with nothing to move, the taps stand still and what would come out is a
  copy of the dry signal a fraction of a window late, which is a comb filter rather than no
  effect.
- **Cents** is the part of a semitone, a hundred either way, so it reaches the next step on its
  own. It has its own knob because a detune is something you dial in by ear, where an interval is
  something you choose.
- **Window** is how long each tap runs before the other one takes over. Short enough to follow a
  drum is short enough to hear as a flutter on a held note, and long enough to hold a note smears
  a drum. There is no setting that does both.
- **Mix** is how much of what you hear has been moved. Kept back with an interval on it, the
  original and the moved copy are a harmony rather than a replacement.
- **Level** is how loud the whole thing comes back, in decibels. The original and a moved copy
  together are louder than either, and this takes that back out without touching the fader.

Under **Double** are two more, both starting at nought, so a shift set up before they existed sounds
exactly as it did.

- **Apart** pulls the two sides away from each other, the left down and the right up by that many
  cents, on top of whatever the interval is. With no interval at all it is a doubler: two copies of
  the sound, a little out of tune with each other, across the room.
- **Feedback** sends what comes out back in, so what was moved is moved again. An octave up with
  feedback is a note climbing out of the top of itself, which is shimmer; an interval down is a
  sound falling away through the floor. What goes back is bent on the way, so however far it is
  turned the cascade holds rather than runs away.

## What it is and is not

This is a delay line, which is the cheap way and the old way. It knows nothing about the sound it
is given: it cannot tell a voice from a cymbal, so it cannot choose where to cut, and the window
is you making that choice by hand. A phase vocoder does know, costs a great deal more, and is a
different effect; a knob here that pretended to be one would not do what it said.

Set a small detune and keep the mix back for a thickener. Set an octave down with the mix full for
a voice that is not yours. Both are the same two taps.

## What ships with it

Nine presets: **Octave Up**, **Octave Down**, **Fifth**, **Detune**, **Small Voice** and **Big
Voice**, which are the plain intervals, then **Shimmer**, an octave up fed back under a low mix,
**Wide Double**, no interval and the sides pulled apart, and **Falling**, a fourth down fed back.
