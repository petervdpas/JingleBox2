# Headroom

How loud a preset should be, and why the level knob cannot tell you.

A preset that reaches full scale on one note is a preset nobody can play a chord on.
The second note is already past the end, and from there every fader, every other track
and the master are working on a signal with nowhere left to go. What somebody hears is
not one loud instrument: it is the whole mix breaking up the moment two things play at
once, with nothing on any page saying why.

That is not a hypothetical. Every preset this application shipped was at or over full
scale on one note, with every level knob sitting at nought.

## Why a level knob cannot answer it

The level is one term in a chain, and the others are not marked in decibels. A drive
squares a saw up and the peak-normalised makeup holds the peak while the loudness
climbs about five decibels. A resonant filter rings at its cutoff and adds several
more, and it sits after the drive, so it is boosting a wave that is already squared
off. An envelope with a fast attack and a high sustain holds all of it.

So a preset can leave at full scale from a level knob reading nought, and there is no
arithmetic on the page that would have told you. The only honest answer is the one the
engine gives, which is why the presets page **plays** the preset and reports what came
out.

## The number

**One note should peak at least 12 dB under full scale.** The reading under the preset
name says where it actually landed, and turns warm when it is louder than that.

Twelve is arithmetic rather than taste. Four notes of equal level sum to twelve
decibels above one when they line up, so a four note chord still arrives under full
scale; eight tracks of unrelated material sum to about nine. It is also roughly where
the broadcast standards put a single signal. EBU R 68 and SMPTE RP 155 set alignment
level, which is where one lone signal is expected to sit, at -18 dBFS and -20 dBFS;
EBU R 128 puts a finished programme at -23 LUFS with a true peak ceiling of -1 dBTP.
None of those is about presets, and all of them say the same thing about them, which
is that one signal has no business being near the top.

## The two switches on a drive

Both of the reasons a preset ends up loud are switches on the machine itself, and both ship
off, so nothing already written sounds any different until you throw one.

**Drive keeps: Peak or Loudness.** A saturation curve is normally paid for by its peak: full
scale in, full scale out. That holds the height of a wave and says nothing about its area, and
a drive squares a wave up, so the knob adds loudness while claiming not to. Measured on a real
patch it was 5.5 dB. On **Loudness** the correction is worked out from the wave the drive is
actually handed, so the knob changes the tone and leaves the level alone.

**Order: Drive first or Filter first.** Drive first squares the wave up and the filter then
takes the top off what it made, which is the screaming filter every tracker has. Filter first
shapes the wave and the drive rounds off what is left. It is a tone control in its own right
and worth having for that alone; what it also does is stop a resonant peak being applied to
something that is already square, which is what pushed this machine's own presets past full
scale.

Together they take the patch that started all this from peaking at 1.06 to peaking at 0.45,
with the drive knob moving the loudness by less than a decibel across its whole range.

**Roaster** and **Sweeper** carry the same switches. An effect cannot be handed the wave it is
about to work on, so its Loudness setting measures what actually went past instead: two running
averages either side of the curve, about fifty milliseconds each. On a steady tone that is
exact.

## It is said, not enforced

Nothing refuses a loud preset. A machine built to be slammed is entitled to exist, and
the person who built it should have to mean it. What the page does is put the number in
front of you at the moment you are choosing it, which is the one moment it is cheap to
change.

The reading goes away as soon as you edit a line, because it is a measurement of the
file rather than of the form. Save the preset and it comes back.

## What has no reading

A preset that plays a recording has none, and that is deliberate. A sampler, a kit and
a recording machine are exactly as loud as the take somebody put on them, and a plugin
is another program entirely. Reporting a number for any of those would send you to
turn down a knob that is not the cause: the level of a take is set on **RECORD**, where
Normalize says where its loudest moment lands.

## Where the room goes

Nothing is lost by leaving it. The mixer's faders, the track's own level and the
master are all still there, and a mix built out of instruments that each left room is
one where those controls do what they are marked as doing. A mix built out of
instruments that did not is one where every control is a distortion pedal.
