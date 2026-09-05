# Roaster

Drive: the signal pushed until it stops being polite. A tilt into a curve into a centring filter,
with the level given back afterwards.

## The controls

- **Drive** is how hard the signal is pushed into the curve, one to twenty four. One is no drive
  at all. The range goes well past useful on purpose: the top of it is a sound rather than a
  mistake.
- **Drive keeps** decides how the curve is paid for. **Peak** holds the height of what comes out,
  which lets the loudness climb with the drive. **Loudness** measures what went past on either
  side of the curve and corrects by that, so what you hear is the tone changing and not the level.
  On a steady tone that correction is exact: at drive 24 the effect goes from nearly nine
  decibels louder to none at all. Peak is the default, since it is what the effect did before the
  switch existed.
- **Tilt** decides which end of the signal gets bitten, minus one to one. Down is weight: the
  bottom of the signal is driven and the result is fat. Up is bite: the top is driven and the
  result cuts. It is a filter in front of the curve rather than an equaliser after it, which is
  why it changes what the distortion is made of rather than what it sounds like afterwards.
- **Bias** leans the signal off centre before it is bitten, plus or minus a half. A curve is
  symmetrical and a leaning signal is not, so the harmonics it makes stop being odd only, which
  is most of the difference between a transistor and a valve. It is taken out again afterwards by
  a filter rather than by subtracting what the curve does to it, since a signal driven hard
  against one side of the curve comes out nearly constant and subtracting the offset would leave
  a step three quarters of full scale.
- **Level** is what comes out, -24 to +12 dB, after the curve has been paid for.
- **Mix** is how much of what you hear is driven rather than what went in. This is the control
  that makes the effect usable on a whole track: a little of something ruined under the original
  is parallel distortion, and it is how a drum bus is made to sound bigger without sounding
  broken.

## Working with it

Turn the drive up until it is obviously too much, then bring the mix down until it is not. That
is the opposite of how it looks and it is the right way round: what the curve does to the quiet
parts of a signal is where the character is, and you cannot hear it while the drive is polite.

The makeup is worked out from what actually went past, so it needs a moment of signal before it
settles. On the first note after silence it does nothing, deliberately, since a ratio of two
numbers that are both nearly nought is noise.

## What ships with it

Six presets: **Warm**, barely there, **Desk**, the sound of something being run a little hot,
**Valve**, biased so the harmonics are not all odd, **Parallel**, a lot of drive under a low mix,
**Megaphone**, tilted up and narrow, and **Destroy**, which is what the top of the range is for.
