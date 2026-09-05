# OddSkilla

An oscillator synth. Nothing is recorded and nothing is loaded: every note is worked out as it
plays, from a wave, an envelope, a filter and a couple of wobbles. That is what makes it the
machine to reach for when you want a sound rather than a sample, and why one patch weighs a few
hundred bytes in a song.

It is polyphonic. A track plays as many notes at once as it has note columns, so a chord needs a
track three or four columns wide.

## The picture at the top

**Shape** draws the wave the oscillator is making, at the settings in front of you, so a duty
knob or a drive can be seen as well as heard. **Cycles** says how many cycles of it are drawn,
one to eight: two is enough to read the shape, eight is useful when you are hunting a phase
trick. **Envelope** beside it draws the amplifier's own attack, decay, sustain and release, which
is the fastest way to tell a pluck from a pad without playing one.

Neither picture changes the sound.

## Oscillator

- **Wave** is the shape: Sine, Square, Saw, Triangle, Pulse and Noise. Sine is a body with no
  edge, square and pulse are the tracker sound, saw is the one that fills a mix, triangle is a
  soft square, and noise has no pitch at all, which is what makes it hats and snares.
- **Duty** is how much of a cycle the pulse wave spends high, 0.05 to 0.95. Half is a square. Away
  from half it thins out and goes nasal, which is a lead cutting through a mix at 0.2 and a
  reedy organ at 0.35. Every other wave ignores it.
- **Tune** is whole semitones added to every note, plus or minus two octaves. Use it to put a
  patch where it belongs rather than transposing what you wrote.
- **Fine** is the last hundredth of a semitone, plus or minus a whole one. Two instruments a few
  cents apart is the oldest thickening trick there is.
- **Pitch env** starts the note that many semitones away and slides into it. Positive falls from
  above, negative rises from below. A kick is a sine with about a two octave drop.
- **Pitch time** is how long that slide takes, up to two seconds. Twenty milliseconds is a click
  on the front of a drum, three hundred is a slide somebody can hear as a slide.

## Amplifier

- **Attack** is how long the note takes to reach full. Two milliseconds is a plucked start; past
  a hundred it is a pad and the front of the note has gone soft.
- **Decay** is how long it takes to fall from full to where it holds.
- **Sustain** is where it holds while the key is down, nought to one. At nought the note is over
  as soon as the decay is, which is how every drum on this machine is made.
- **Release** is the tail after the note is let go of.
- **Drive** pushes the wave into a saturation, one to ten. It fills the tone out and squares it
  off. What it does to the level depends on the switch beside it.
- **Drive keeps** is that switch. **Peak** holds the height of the wave, which is what this
  machine always did and which quietly adds loudness as the drive comes up, about five and a half
  decibels by the top. **Loudness** works the correction out from the wave itself, so the drive
  changes the tone and leaves the level alone. Peak is the default because every song written
  before the switch existed was made against it.
- **Level** is the instrument's own output, -60 to +6 dB, under the pattern's volume column and
  before the mixer.
- **New note** says what happens to a note still sounding when the same column plays another:
  **Cut** ends it at once, which is what a tracker has always done, **Release** lets it fall away
  through its release, and **Sustain** leaves it alone entirely. Release and Sustain are what a
  piano or a pad wants; Cut is right for anything percussive.

## Filter and modulation

- **Order** decides whether the drive runs into the filter or the filter into the drive. Drive
  first squares the wave up and then takes the top off it; Filter first shapes it and then bites
  what is left. They are two different instruments, which is why the switch is on the front, and
  Filter first also stops a resonant peak being applied to a wave that has already been squared
  off.
- **Cutoff** closes a low pass, one is open and nought is shut. It is the tone control for the
  whole machine.
- **Resonance** rings at the cutoff, up to 0.98. Past about 0.7 the ring is the sound rather than
  a colour.
- **Vib rate** and **Vib depth** wobble the pitch: five to seven hertz at twenty cents is a
  singer's vibrato, and anything above ten hertz stops being expression and starts being an
  effect.
- **Trem rate** and **Trem depth** wobble the level instead, the same range and the same rule.

## What ships with it

Twenty presets, from Kick, Snare, Hihat and Clap through Bass, Sub, Reese and Pluck to Pad,
Strings, Bell and Sweep. They are a starting point rather than a library: every one of them is
your patch the moment you turn a knob, since a song owns its instruments.

Each of them leaves about twelve decibels of headroom on one note, deliberately. Four notes of
equal level sum to twelve decibels above one, so a four note chord still arrives under full
scale. A preset you build yourself is yours to make as loud as you like, and the designer's
presets page will tell you how much room it left.
