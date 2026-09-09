# Widener

Takes a sound that sits in the middle of your head and puts it across the room instead. It is for
a mono source: a microphone, a guitar, anything that arrived on one channel.

## Why a mono input needs this at all

A one channel recording is already stereo by the time it reaches a chain here. The input is read
into both sides by copying, and every take is written with two channels, because an effect places
things in the stereo field and narrowing the answer would throw away half of what it did.

But two identical channels are dead centre, and no fader can change that. A pan moves the whole
thing to one side; it does not open it. What makes a sound wide is the two sides being
**different**, and that is the only thing this effect does.

## How it opens it

It works on the middle and the side rather than on the left and the right, and that is the whole
of why it is safe. What comes in is split in two: the middle is what both sides share, and the
side is what they differ by, which for a mono source is nothing at all. A side signal is then
made out of the middle, and the two are put back together as middle plus side and middle minus
side.

Added back together that side cancels **exactly**, by arithmetic rather than by luck. So a
Widener with the Haas knob at nothing folds down to precisely the signal that arrived: the mono
version of a widened recording is the recording.

## The knobs

**Width** is how much side signal is made, and it is the amount knob. At a quarter the side is
0.147 of what came in, at a half 0.294, and at the top 0.588. For scale, two channels with no
relation at all measure 0.707, so the top of the knob is about five sixths of the way there.

**Depth** is how far the two taps travel, and it is **not** the amount, which was measured rather
than assumed: the side is 0.147, 0.146 and 0.145 at one, three and five milliseconds. Two copies
of a signal are unrelated at any separation past a few samples, so the travel has almost nothing
to do with how wide it sounds. What it changes is the character of the movement, which you hear on
sustained sounds and barely at all on a drum.

**Rate** is how quickly those two lengths move. Slow is a picture that drifts, which is what a
voice wants. Fast enough and it stops reading as width and starts reading as movement, which is a
chorus rather than a placement.

**Haas** is a plain delay on one side, and it is the strongest way there is to place a sound: the
ear decides where something is by which side reached it first, so a few milliseconds is heard as a
direction and not as an echo. It is also the one control here that breaks the promise above. Heard
in mono the two arrivals add together and comb whole bands out of the sound, and a broadcast, a
phone and a single speaker are all exactly that addition. So it is off unless you turn it on, and
if you turn it on, listen to the result folded down to mono before you keep it.

**Haas on** decides which side that delay goes on, which decides which way the sound leans. The
side without the delay is the one the ear hears first, so a delay on the right leans the sound to
the left.

**Mix** is how much of what you hear has been opened. What it puts back is the signal exactly as
it arrived, so this is the one control here that makes the picture narrower again.

## What it costs

Opening a sound costs peak level, because the two sides are the middle plus and minus the side,
and the loudest of them rises as the side grows. Measured: about **1.8 dB** at a quarter width,
**3.4 dB** at a half, and **5.9 dB** at the top.

That is what widening is rather than a fault in it, and nothing here refuses it. But it is worth
knowing before you put one on a master, and it is the reason a fresh one sits at a half.

## What it will not do

It will not make a stereo recording narrower. Every knob adds difference and the side that arrived
is carried through untouched, so a signal that already had a picture keeps it and is opened
further. Narrowing is the same arithmetic with the side scaled down, and it is a different effect.

It will not turn one sound into two. There is nothing here that invents a second performance: what
comes out is the same sound arriving at two slightly different moments, which is what width
actually is.

## Where to put it

On any chain: a track, a pad, the master, or the recording input. On the input it is heard while
you play, and the take saved under your own name is the widened one; the take beside it marked
clean is the mono signal exactly as it arrived, so nothing is lost by trying it.

On the master it will open a whole mix, which is worth knowing and is rarely what you want: a mix
already has width, and more side on top of it mostly smears the middle.
