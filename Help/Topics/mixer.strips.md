# Mixer

Everything this application plays, in strips: level, placement, mute, solo and ducking.

## Three groups, and they are not the same kind of thing

**TRACKER** is one strip per song track. Solo silences every track that is not soloed,
and mute beats solo on the same strip. Touching a strip anywhere picks its track, so
the pattern, the chain and the automation follow what your hand is on.

**RECORDER** is two: **IN**, what is coming in to be recorded, and **PLAY**, a take
being auditioned on RECORD against the rest of the mix. **PADS** is every pad
together.

IN is the desk's input channel and its fader is the input's own gain, so it decides
what a take holds. At the foot of it are the source it is listening to, **Hear it**,
which puts what is coming in through the Recording Effects chain and out of the
master, and **Only here**, which takes that source off its own output so it is heard
through this application and nowhere else. Its mute, its placement and a solo are
about what Hear it is sending; with that off, nothing of it is in the mix.

The difference matters when you pull a fader down. A track is the song's, so moving it
changes the song and is saved with it. RECORDER and PADS are this installation's, so
moving one changes how this machine sounds and not what anybody else would hear from
your `.jibx`.

The side chain at the bottom of a strip ducks that track while another one sounds:
pick the track to listen to, how far down this one goes, and how long it takes to
come back. The attack is always fast, because a slow one leaves the kick fighting
the track it is meant to be clearing room for.

## The master

The strip on the right is a strip without being a track. It has a level, a place and
one effect chain the whole song goes through, and it is applied in that order with
the saturation last, so the fader cannot put the mix back outside it.

It has no solo, since soloing everything is what it is already doing, and no ducking,
since everything is summed by the time it is reached. Its meter reads what is
leaving, which makes it the one meter on the page measuring what you actually hear.

Its effects and its automation fold open underneath it, and they are the master's own
rather than the ones under the pattern, which follow wherever the cursor is.

## Meters

A track's meter is worked out from the voices sounding on it, so it falls on its own.
The master's is a peak off the last buffer, so it says nothing once it is older than
a moment: a reading that stayed lit after the music stopped would be reporting the
last thing that played for the rest of the session.

## Where it is kept

The mix is part of the song and is saved with it. Moves are heard straight away, even
in the middle of a take. A note played by hand goes through the track's own fader,
mute and placement, so what you hear under your fingers is what the part will sound
like.
