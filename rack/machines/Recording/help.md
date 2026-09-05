# Recording

One of your recordings, played from the keyboard and pitched by resampling. The plainest machine
on the rack and the one to reach for when what you want is a take rather than an instrument: a
voice line, a jingle, an atmosphere, a single hit.

Everything Zampler does across a whole keyboard, this does with one file. If you want several
recordings under one instrument, that is Zampler; if you want one recording per key with no
transposing, that is BongaBong.

## Sample

- **Take** is the recording. **Pick a recording** chooses one off RECORD's shelf, so the
  application owns every file an instrument depends on and a song can be packed and handed on.
- **Base note** is the key at which it plays untouched. Every other key reads it faster or
  slower, so a take recorded at A plays a fifth up at E whatever it is a recording of.
- **Start** and **End** are the window that actually plays, as a fraction of the file. Trim the
  silence off the front here rather than in an editor: the file is untouched and every
  instrument using it can window it differently.
- **Loop** is **None**, **Forward**, or **Ping-pong**, which turns round at each end and is the
  gentler of the two on a sustained tone.
- **Loop start** and **Loop end** are where the loop sits inside the window. A held note plays to
  the loop end and goes back to the loop start for as long as the key is down.
- **Direction** plays the window backwards. A reversed cymbal into a downbeat is the oldest trick
  there is and it costs nothing here, since the file is not touched.
- **Voices** decides whether it piles up or not. Many is an instrument; one cuts what it was
  sounding before it starts the next note, which is right for a long take that would otherwise be
  four copies of itself playing at once.
- **New note** says what happens to a note still sounding when the same column plays another:
  **Cut**, **Release** or **Sustain**.

## Envelope

**Attack**, **Decay**, **Sustain** and **Release** shape the loudness on top of whatever shape
the recording already has. Sustain at full and a short attack is the machine keeping out of the
way, which is where it starts.

## Pitch and modulation

- **Tune** is whole semitones, plus or minus two octaves, and **Fine** is the last hundredth of
  one.
- **Vib rate** and **Vib depth** wobble the pitch. Five to seven hertz at twenty cents is a
  singer's vibrato; on a spoken take a little of it is the difference between a sample and a
  performance.
- **Pitch env** starts the note that many semitones off and **Pitch time** is how long it takes
  to arrive. A short drop into the note is a tape start; a long rise is a machine winding up.
- **Trem rate** and **Trem depth** do the same to the level.

## Filter

**Cutoff** and **Resonance**: a low pass over the whole thing, open at one and shut at nought.
Useful for putting a take behind something rather than in front of it.

## Output

- **Level** is the instrument's own, -60 to +6 dB.
- **Drive** pushes it into a saturation, one to ten. On a clean voice take a little of it is a
  cheap radio; a lot of it is a broken one.

## What ships with it

One preset, Your recordings, which is a pointer at the shelf you already have rather than a sound
of its own. Nothing else would make sense here: the machine is whatever you recorded.
