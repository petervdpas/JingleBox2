# Operetta

An FM synth: four sine operators bending each other's frequency. It is the machine for the sounds
a filter cannot make: electric pianos, bells, glassy pads, hard basses, brass and clangs.

## How it makes a sound

A sine on its own is the plainest sound there is. Bend its frequency with another sine slowly and
you hear vibrato. Bend it fast, at the speed of a note, and you no longer hear a wobble at all:
you hear new partials either side of the note, and the harder it is bent, the more of them there
are. So two sines make anything from a flute to a saw to a bell, and turning one knob moves the
whole spectrum at once. That is frequency modulation, and it is the sound of the Yamaha DX7.

Each of the four is an **operator**: a sine, a ratio, a level and an envelope. An operator that is
heard is a **carrier**. An operator that only bends another is a **modulator**, and its level is
not loudness but brightness. Which is which is the algorithm's to say.

## Voice

- **Algorithm** is how the four are wired. An arrow is one operator bending the next, a plus is
  two together, and only what is at the end of a line is heard. **4→3→2→1** is one long stack
  and the most complex; **1+2+3+4** is four plain sines side by side, which is an organ. The
  ones between are two stacks, one modulator bending three carriers, or three bending one.
  Operators are numbered so that a modulator is always higher than what it bends.
- **Feedback** feeds the fourth operator back into itself. A little turns its sine towards a saw,
  which is where brass and bass get their edge. The top of the knob starts to hiss.
- **Frequency** is whole semitones added to every note, two octaves either way, and **Fine** the
  last hundredth of one.
- **Volume** is how loud it comes out. Four operators heard at once are shared out rather than
  added up, so changing algorithm does not make it jump in level.
- **New note** is what happens to the note a track is still sounding when the next lands on it:
  **Cut** stops it, **Release** lets it ring out underneath, **Sustain** holds it until an OFF.

## Each operator

- **Ratio** is how many times the note's frequency the operator runs at, from half to sixteen.
  Whole numbers are harmonic: a modulator at two over a carrier at one gives the harmonics of a
  saw, at three the hollow ones of a square. Anything between whole numbers, like **3.5** or
  **1.41**, is inharmonic, and that is where bells and metal come from.
- **Fine** moves it a few cents. Two carriers a few cents apart beat slowly against each other,
  which is chorus without an effect.
- **Level** is how loud the operator is when it is heard, or how hard it bends when it is a
  modulator. On a modulator this knob is the brightness.
- **Attack**, **Decay**, **Sustain** and **Release** shape that level over the note. On a carrier
  that is the loudness of the note. On a modulator it is the brightness over time, which is the
  whole trick of FM: a modulator with a short decay is a note that strikes bright and settles to a
  pure tone, which is a piano tine, a pluck or a bell depending on the ratios.

A note ends when every operator that is heard has faded out. A modulator still ringing under a
carrier that has gone is bending nothing anybody can hear.

## Working with it

Start from two operators. Pick an algorithm where the second bends the first, set both ratios to
one and bring the second's level up slowly: the sine becomes brighter and brighter. Then change
the second's ratio to two, three and three and a half, and listen to it turn from a saw to a
square to a bell. Give the second a short decay and the brightness becomes a strike.

Levels on modulators matter much more than levels on carriers. A tenth of a turn on a modulator is
the difference between warm and harsh.

## What ships with it

Nine presets: **Init**, the plainest two operator sound, **Tine Piano**, the classic electric
piano with its bright strike, **Glass Bell**, two inharmonic stacks ringing for seconds, **Solid
Bass**, a fed back stack two octaves down, **Brass**, slow modulators opening under three at once,
**Drawbar Organ**, four sines side by side that hold until an OFF, **Clang**, a stack of inharmonic ratios falling away,
**Slow Pad**, three carriers drifting apart under one slow modulator, and **Wood Pluck**, a short
knock of a note.
