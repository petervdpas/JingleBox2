# Clock

Whether the transport keeps its own time or follows another machine's, and which outputs are sent
this machine's clock.

## Two settings, not one

They look like two halves of a master-or-slave switch and they are not, because they are
independent. Whose clock you run on has exactly two answers and you have to pick one. What you
send clock to is a list, and it has nothing to do with the first: a machine keeping its own time
may drive two devices, and a machine following someone else's may pass that clock on to a third.

## Its own clock

Off is what every song has played on until now: the tracker keeps time with a stopwatch of its
own and answers to nothing outside. The tempo in the song is the tempo.

## Following another machine

On, the transport runs on MIDI clock arriving at one port. One port and not several — a transport
following two clocks is following neither.

With no port chosen, or one that is not plugged in, the transport stays on its own clock rather
than refusing to play. A cable left in the other room is not a decision to stop working, and the
line at the foot of the card says which of the two is actually happening.

The port is remembered when you turn following off, so turning it back on does not make you find
it again.

## Sending clock

Tick any output to send it this machine's clock. Nothing is sent until you do: clock arriving at a
device nobody pointed it at is a device that starts running when its owner did not ask.

What goes out is the standard's four messages. Twenty four ticks to the quarter note, which is
what every sequencer ever built assumes. Start when the transport begins at the top, and stop when
it stops. Starting from anywhere but the top sends a **song position pointer** first and then a
continue rather than a start, which is what stops the rest of the desk playing from its own bar
one while you play from line 32.

An output that will not open is left out and said in the log. An output that stops answering later
is kept and tried again, so a cable knocked out and put back comes good on its own without a trip
back here.

## What it costs

Almost nothing, and it was measured rather than assumed. Sending one tick to a real port takes
about a quarter of a millisecond, against the 20.8 ms a tick lasts at 120 to the minute. Opening
the port is the expensive part at 80 ms or more, so it happens when you tick the box and never
while the music is running.

Ticks are placed within about a tenth of a millisecond of where they belong. What happens after
that — the operating system's own buffering, and a MIDI cable that carries a byte in 320
microseconds — is beyond anything this program can see or control.

## Lines and ticks

A tick is a fixed division of a beat and a tracker line is not, so the two are related rather than
counted against each other. At four lines to the beat a line is six ticks and at eight it is
three; at five it is 4.8 and at seven 3.43, neither of them whole. Nothing here counts ticks per
line for that reason: each line's position is worked out from its own number, which stays exact at
every setting rather than drifting by a fraction of a tick per line.
