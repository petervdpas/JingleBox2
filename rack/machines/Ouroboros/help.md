# Ouroboros

A mono synth: one oscillator, a noise source beside it, a filter that can be swept, and glide
between notes. It is the machine for a bass line that slides, a lead that never plays two notes
at once, and drones.

Monophonic on purpose. A track playing it sounds one note at a time however many note columns it
has, and a new note takes over the voice that is already running, which is what glide needs to
exist at all.

## Oscillator

- **Wave** is Saw or Pulse. With one oscillator this is most of the character: saw is the full
  one that fills a mix, pulse is the hollow one that sits under other things.
- **Pulse width** is how wide the pulse is, 0.02 to 0.98. Half is a square; away from half it
  thins out. The saw ignores it.
- **Frequency** is whole semitones added to every note, plus or minus two octaves.
- **Fine** is the last hundredth of a semitone.
- **Glide** is how long the pitch takes to travel from the note before it to the new one, up to
  two seconds. Nought is off. Sixty to a hundred and fifty milliseconds is a bass that slides
  between notes without sounding drunk. Glide is what a monophonic synth is for.

## Mixer

- **Mix** fades noise in beside the oscillator, nought to one. A mixer rather than a choice of
  wave, deliberately: a kick wants a sine body with a noise transient over it and a snare wants
  mostly noise with a little tone underneath, and neither is possible if picking noise means
  giving the oscillator up.

## Filter

- **Cutoff** is where it turns over, 20 Hz to 20 kHz.
- **Resonance** is how hard it rings there, up to 0.98. Past about 0.8 with the cutoff moving is
  where an acid line lives.
- **Mode** is **LowPass**, which takes the top off, or **HighPass**, which takes the bottom out.
  A high pass with the resonance up is a thin, papery sound that sits over a mix rather than in
  it.

## Amplifier

- **Amp** decides whether the envelope opens the amplifier at all. On, a note has a shape. Off, a
  note is simply on at full and off again, which is the old two operator sound and is right for
  blips and clicks.
- **Volume** is how loud it comes out, nought to two, and half is the sensible place. A raw saw is
  already a full scale wave, so arriving at one means arriving clipped against everything else in
  the mix.
- **New note** says what happens to the note still sounding when another arrives: **Cut**,
  **Release** or **Sustain**. With glide in use, Cut is what you want, since the voice is being
  taken over rather than replaced.

## Envelope

One envelope, and it serves both the amplifier and the filter.

- **Attack** is how long the note takes to reach full, up to four seconds.
- **Sustain** is a switch rather than a knob. Off is a drum or a pluck: the note falls away
  whether or not the key is held. On is anything held. Two settings cover more than a sustain
  knob suggests and there is nothing to get wrong.
- **Decay** is how long it takes to fall away, and how long the tail is after the note is let go
  of, up to eight seconds.

## LFO

- **Rate** is how fast it runs, from a hundredth of a hertz to a hundred. Above about twenty it
  stops being a wobble and becomes a tone of its own, which is why the range goes so far.
- **Wave** is Triangle, which is a wobble, or Square, which is a trill.

## Moves the oscillator

- **Source** is the **Envelope** or the **Lfo**.
- **Amount** is how much, nought to one. Nought is the route switched off.
- **What** is where it lands: **Frequency**, which is vibrato from the LFO and a pitch sweep from
  the envelope, or **PulseWidth**, which is the width wobbling and is the thickest thing this
  machine can do with one oscillator.

## Moves the filter

- **Source** is the **Envelope**, which is the usual answer, or the **Lfo**.
- **Amount** is how far it moves the cutoff, nought to one.
- **Polarity** decides which way. One way the envelope opens the filter, which is the classic
  sweep; the other closes it, which is a note that gets duller as it holds.

## What ships with it

Eight presets: Init, Mother Bass, Glide Lead, Acid, Sub Drone, Wind, Blip and Thump. Init is
deliberately plain and is where to start something of your own.

There is no drive on this machine and no switch about one, which is not an oversight: the chain
is oscillator, filter, amplifier, so there is nothing to level out and nothing to reorder.
