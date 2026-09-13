# Lighttower E=mc²

A drawn-wave synth, after the Fairlight CMI. You draw the first wave of a sound and the last
one with the pointer, and the machine works out the thirty waves between them. Every note goes
through all thirty two, so a sound can start as one shape and end as another.

## How it makes a sound

On the Fairlight a voice held thirty two waveforms, and it played them one after the other while
the note sounded. Nobody drew thirty two by hand. You drew the first and the last with the light
pen and told the machine to merge the rest, and it filled them in point by point in a straight
line from one to the other. That is exactly what happens here.

Draw a soft sine as the beginning and a jagged saw as the end, and each note starts round and
grows teeth. Draw the same wave at both ends and the sound stays still, like a plain oscillator
you drew yourself.

## Waves

- **Begin** and **End** choose which wave is on the pad. The one you are not drawing shows
  faintly behind it, so you can see where the sound starts from and where it is going.
- **Draw** by pressing on the pad and moving. A quick stroke fills every point it crosses. The
  stroke is kept when you let go, so each stroke is one step of undo.
- **Sine**, **Triangle**, **Saw** and **Square** put a plain shape down on the wave in hand, to
  start from or to get back to.
- **Smooth** takes the corners off the wave in hand. A wave drawn by hand is full of tiny steps,
  and those are heard as buzz; press it a few times for a rounder sound.
- **Clear** wipes the wave in hand flat, so you can draw it again from nothing. It is one press,
  so undo brings the old wave back.
- The **stack** beside the pad is every wave the sound goes through, the first at the front and
  the last at the back, the way the Fairlight drew them. It follows your hand while you draw.

A wave drawn above or below the middle line is still heard centred, so a lopsided drawing does not
thump when a note starts and stops.

## Voice

- **Sweep** is how long a note takes to get from the first wave to the last.
- **Motion** is what happens then. **Once** stays on the last wave for as long as the note is
  held. **Loop** jumps back to the first and goes round again. **Bounce** comes back the way it
  went, over and over, which is a sound that breathes.
- **Grit** is the Fairlight's own sound: eight bit points read without smoothing, and a note that
  only steps on to the next wave when the wave comes round to its start. That stepping is part of
  the character. **Smooth** glides between the waves and between the points instead, which is
  cleaner and less like the machine.
- **Frequency** is whole semitones added to every note, two octaves either way, and **Fine** the
  last hundredth of one.
- **Volume** is how loud it comes out, before the track's own level.
- **New note** is what happens to the note the track is still sounding when the next one lands on
  it: **Cut** stops it, **Release** lets it fade under the new one, and **Sustain** leaves it
  holding until an OFF.

## Envelope

**Attack**, **Decay**, **Sustain** and **Release** are four faders that shape how loud a note is
over its life. The picture beside them draws the shape, with a dashed line where the key is let go.

## Presets

The presets are starting points, each a pair of drawn waves and a way through them. Pick one and
draw over it: the waves are saved with the instrument in the song, and **Save** on the preset
picker keeps your drawing as a preset of your own.
