# Clock

Whether the transport keeps its own time, follows another machine's, or shares an Ableton Link
session, and which outputs are sent this machine's clock.

## Two settings, not one

They look like two halves of a master-or-slave switch and they are not, because they are
independent. Whose clock you run on has exactly three answers and you have to pick one. What you
send clock to is a list, and it has nothing to do with the first: a machine keeping its own time
may drive two devices, and a machine following someone else's may pass that clock on to a third.

## Its own clock

The tracker keeps time with a stopwatch of its own and answers to nothing outside. The tempo in the
song is the tempo. Every song plays on this unless you choose something else.

## Following another machine

The transport runs on MIDI clock arriving at one port. One port and not several, since a
transport following two clocks is following neither.

With no port chosen, or one that is not plugged in, the transport stays on its own clock rather
than refusing to play. A cable left in the other room is not a decision to stop working, and the
line at the foot of the card says which of the two is actually happening.

The port is remembered when you turn following off, so turning it back on does not make you find
it again.

While the master's clock is running, its tempo is worked out from the ticks and put in the song's
tempo, so the BPM field says what is really playing and turning the tempo on the master moves it
here within a couple of seconds. It changes the song like typing a tempo would, so it can be undone
and is saved with the song. A master that sends clock only while it plays, as a KeyStep Pro does,
leaves the field on its last number while it is stopped.

## Ableton Link

Link shares a tempo, the position in the bar, and optionally play and stop with every Link program
on the same network: Live, Bitwig, Reason, Traktor, many phone apps, another computer running this.
There is no master. Anybody can change the tempo and everybody follows, and programs can join and
leave whenever they like.

Choosing it is what joins the network, and that is the only time anything is sent. On Windows the
firewall asks about it the first time. The line at the foot of the card says how many other
programs are in the session.

**It does not carry notes or song positions.** Link lines up beats and bars, so two programs start
bar one together; it cannot tell another program which pattern or line you are on.

**Starting.** Alone, play starts at once. With anybody else in the session, play waits for the
next bar so it lands in time with them. How many beats make that bar is **Beats that line up**:
four for a bar of four, three for a waltz, eight or sixteen to wait for a phrase.

**Play and stop.** With **Share play and stop** ticked, pressing play or stop here starts and stops
the other programs, and theirs starts and stops this one. It only reaches programs that have it
switched on too.

**Tempo.** The session's tempo is the song's while you are on Link. A tempo another program sets is
written into the song's tempo, like a tempo arriving from a followed MIDI clock, so it can be undone
and is saved. A tempo you type, and a tempo lane, are put to the session and everybody follows.

**Lining up by ear.** This machine starts each line early by as long as its own output takes to
reach the sound card, its buffer and the mixing cushion in SETTINGS, Engine. What happens after the
sound card (its converters, a Bluetooth speaker, a long cable) it cannot know. If this machine
sounds late against the others, raise **Output offset**; if it sounds early, lower it, below nought
if need be. A few milliseconds at a time is the right size of step.

Link stays tight over wifi where MIDI clock does not, because it shares a timeline rather than
sending ticks: a late network packet changes how quickly the programs agree, not when a beat
sounds.

A build without the Link library offers the choice greyed out and says why. A settings file that
names Link on such a machine plays on its own clock rather than refusing to play.

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

On Ableton Link the outputs are sent clock at the session's tempo, started and stopped with the
transport, which is how a drum machine or a synth with only a MIDI clock input follows a Link
session.

## Passing a clock on

Following a clock and sending one are not exclusive, and the case where you want both is ordinary:
something else holds the time, this machine runs on it, and a third device is plugged into this one
because there is nowhere else to plug it. Tick the output and it gets the clock.

What it gets is the clock that arrived, passed straight on. Every tick, every start, continue and
stop, and every song position pointer, put out again exactly as it came in. Nothing is worked out
again on the way through, so a relocation lands on the same beat at both ends and a chain of
machines does not drift a little further at each link.

While you are following, the master owns the transport, so pressing play here does not announce a
start to the devices you drive: what they hear is what the master said. Turn following off and this
machine is the clock again, sending its own.

## The one output you cannot drive

A device with a MIDI in and a MIDI out shows up as a name in both lists, and the obvious way to set
a chain up is to follow it and tick it. That would send its own clock straight back at it, which on
a device that takes clock as well as sending it is a loop neither end can see.

So the port you are following is left out of what is driven, whether or not it is ticked, and the
line at the foot of the card says so when it happens. The tick is left where you put it: turn
following off, or follow something else, and that output is driven again.

## What it costs

Almost nothing, and it was measured rather than assumed. Sending one tick to a real port takes
about a quarter of a millisecond, against the 20.8 ms a tick lasts at 120 to the minute. Opening
the port is the expensive part at 80 ms or more, so it happens when you tick the box and never
while the music is running.

Ticks are placed within about a tenth of a millisecond of where they belong. What happens after
that (the operating system's own buffering, and a MIDI cable that carries a byte in 320
microseconds) is beyond anything this program can see or control.

## Lines and ticks

A tick is a fixed division of a beat and a tracker line is not, so the two are related rather than
counted against each other. At four lines to the beat a line is six ticks and at eight it is
three; at five it is 4.8 and at seven 3.43, neither of them whole. Nothing here counts ticks per
line for that reason: each line's position is worked out from its own number, which stays exact at
every setting rather than drifting by a fraction of a tick per line.
