# Sweeper

A filter with a drive into it: four poles, three modes, and a cutoff that glides. What a
synthesiser does to one voice, done to a whole track.

## The controls

- **Cutoff** is where it turns over, 20 Hz to 20 kHz. It glides rather than jumps, and it glides
  in cents rather than in hertz, which is the only way a sweep sounds even: a filter moving
  evenly in hertz crawls through the two octaves anybody is listening to and leaps through the
  eight nobody is.
- **Reso** is how hard it rings at the cutoff, up to 0.98. Past about 0.7 the ring is the sound.
  At the top it is a whistle with your track behind it, which is either exactly what you wanted
  or a mistake you will hear immediately.
- **Drive** pushes the signal into the poles, one to eight. A filter with something hot going into
  it is a different filter, which is what makes this more than a tone control.
- **Drive keeps** decides how the drive is paid for. **Peak** holds the height of what comes out,
  which means the loudness climbs as the drive comes up. **Loudness** measures what went past on
  either side of the curve and corrects by that instead, so the drive changes the tone and leaves
  the level where it was. Peak is the default because it is what the effect did before the switch
  existed.
- **Order** is whether the poles run before the drive or after it. **Drive first** bites the
  signal and then filters what is left, which is the dirtier of the two. **Filter first** shapes
  it and then bites, which keeps a resonant peak from being applied to something already squared
  off.
- **Mode** is **Low**, which takes the top off, **Band**, which keeps a strip and throws both ends
  away, or **High**, which takes the bottom out. Band with the resonance up is a telephone; High
  is how a part is made to sit over a mix instead of in it.
- **Mix** is how much of what you hear is filtered rather than what went in. Below one you are
  blending the filtered signal with the original, which is a way to take the top off something
  without losing its body.

## Working with it

The two switches matter more than they look. A sweep with the drive up and Peak selected gets
louder as it closes, which reads as the filter doing something exciting when it is really just a
level; put it on Loudness and you hear what the filter is actually doing.

Point a knob at the cutoff and the resonance and the whole effect becomes something you play
rather than set. Ctrl+Shift+M with the pointer on the knob, then touch the control on your desk.

## What ships with it

Six presets: **Open**, which is the effect out of the way, **Telephone**, a narrow band,
**Under Water**, a low pass with the ring up, **Take The Mud**, a high pass that clears the
bottom of a part, **Sing**, resonance well up and ready to be swept, and **Nightclub**, which is
what a track sounds like through a wall.
