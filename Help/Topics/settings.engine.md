# Engine

The rate, the buffer, and how the sound is kept fed.

The rate is what the tracker, the synth and any plugins on them run at.

Following the output device is the right answer almost always: the audio is not
resampled on its way out, and a plugin is told the rate it is really being fed at.
A plugin built for one rate and fed another has its filters and timings in the wrong
place, which is what the "samplerate mismatch" messages some plugins print are about.

A rate cannot change while the app is running. Voices, envelopes, filters and every
loaded plugin work their timings out from it once, so a change takes effect the next
time the app starts. It is the only setting here that waits.

The output buffer is how much audio the sound card holds ahead of what you hear, so
it is also the latency: what is playing was mixed that long ago, and it is how long a
key waits before it sounds. Small is tighter to play and gives the mixing less room to
be late in; too small for the machine and what comes out has holes in it. It is shown
in frames, which is what every other audio application calls it, with the milliseconds
beside it, since 512 frames is 12 ms at 44100 and 11 at 48000.

How often it is topped up and how many threads do the topping go with it. One thread
fills every stream in the application in turn, so a pad decoding a file can delay the
tracker; more than one lets a slow stream stop holding up the others. Past four they
wake to look at buffers that are already full.

The plugin cushion is the same question at the other end. A plugin runs in a process
of its own and every block it plays is a message out and a message back, made from the
thread that has milliseconds to fill a buffer. A cushion moves that work onto a thread
of its own, so a plugin being late eats into the queue instead of into the output, and
it costs exactly what it says between playing a note and hearing it.

Those four take effect at once: the output is closed and opened again as you change
them, so the right value can be found by listening rather than by restarting between
guesses.

Three switches sit under them and none is about how much audio is held.
**Real-time audio** asks the machine to let the threads that must not be late take
their turn ahead of everything else, so a browser laying out a page cannot delay the
sound. **One output stream** sums the tracker, the pads and a take being auditioned
into a single stream rather than handing the sound card one each.

**Fast drive curve** is the third, and it is the only setting here that is about
arithmetic. Every drive in this application, on a machine and inside an effect, bends
its signal through the same curve, and asking the system for that curve is a call that
stays a call however fast the rest of the mixing gets. At the mixer's own ceiling of
forty eight voices it is over half of what a rich patch costs. With this on the curve
is read off a table drawn once at startup, which is about six times cheaper for every
sample of every sounding voice.

The two curves are 161 decibels apart at worst, which is below the steps a sample has
once it is written out, so a driven note rendered either way is the same note at the
output. It is a speed setting rather than a sound, and it is off unless you turn it on,
which is the rule everything on the audio path here keeps: what shipped is what you
have until you have listened to the other thing. Turn it on if notes break up on a busy
song. It lands inside the block being mixed, so you can sit with a song playing and
throw it back and forth.

It is here rather than on a machine's own face on purpose. A machine's **Drive keeps**
and **Order** are facts about the sound, saved with the instrument and carried in the
song; this is a fact about how much time this computer has, like the buffer sizes above
it. A song that sounded different on two machines for a reason nobody chose is what a
knob here would have bought.

## ASIO

On Windows the output picker lists ASIO drivers as well as the system's own outputs.
ASIO is Steinberg's driver standard, and the point of it is that the system's mixer
is not in the path: the buffer is the card's own, so the delay is a few milliseconds
rather than the twenty a shared path costs.

How big an ASIO block is belongs to the driver, not to the slider above: what the
card's own panel is set to is what it runs, and the reading under the device says
what that turned out to be. The rate is asked for rather than insisted on, since a
card clocked from something else will refuse, and the mix is resampled into whatever
it is really on.

An empty list means one of two things and the page says which: no ASIO on this system
at all, or ASIO present with no driver installed, which is most Windows machines until
a card's own driver or something like ASIO4ALL puts one there.

**A driver needs One output stream, the switch above.** The driver owns the card and
can be handed one thing, so everything this application plays has to be summed before
it leaves. With the switch on, the tracker, the pads and a take being auditioned all
go out of the driver. With it off, the tracker is handed to the driver on its own and
the pads and RECORD play into a device that is not connected to anything, which is
silence with nothing to explain it.

**If the sound goes strange after changing one, restart the app.** Reopening the
output while everything else is still running is not the same as starting clean:
plugins are still loaded, threads are already going, and the sound card has been
handed back and taken again. The setting itself is remembered, so a restart costs
nothing but the wait, and it is worth trying before concluding that a value is bad.
