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

**If the sound goes strange after changing one, restart the app.** Reopening the
output while everything else is still running is not the same as starting clean:
plugins are still loaded, threads are already going, and the sound card has been
handed back and taken again. The setting itself is remembered, so a restart costs
nothing but the wait, and it is worth trying before concluding that a value is bad.
