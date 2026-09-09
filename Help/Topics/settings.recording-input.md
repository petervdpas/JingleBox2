# Recording input

Where the RECORD tab captures from.

Pick the device the RECORD tab captures from. The choice is remembered between
sessions.

On Linux this can go further than a device: the source picker offers the programs
that are playing, so a browser can be recorded on its own. That is PipeWire, which
treats every stream as something that can be patched.

On Windows the same picker offers each output through WASAPI loopback, which records
everything that output is playing, **and each program that is playing on its own**. So a
browser can be recorded by itself there too. That is per-process loopback, which needs
Windows 10 build 20348 or later; on an older one the programs are simply not in the list
and the devices and outputs are.

A program is offered while it is making a sound, so one that is paused is not there and
turns up the moment it plays.

## Only here

Capturing a source leaves it playing wherever it was playing. That is what every program
that records does and it is right for streaming; on air it is wrong, since what is going
out must not also be coming out of the desk speakers a moment later.

**Only here** is the switch under the source picker, and it is a second act rather than
something choosing a source implies. Thrown, the source is taken off its own output and
reaches JingleBox2 alone. It is off unless you ask for it, since it changes another
program rather than this one, and it is put back when you pick another source or close
the application.

**On Windows it needs somewhere to send the source**, since there is no link to unplug:
a program can only be pointed at another output. **Send it to** above the switch is where
that is chosen. Until one is picked the switch stays grey, and resting the pointer on it
says which of the two reasons it is grey for.

**And on Windows the switch does not work at all at the moment.** It reaches per-program
output through an interface of Windows own that this application can no longer call: the
marshalling it needs was taken out of .NET, and the switch has therefore never worked on
this platform. The log says so on every start. It is not your machine and it is not a
setting: the interface is there and answers, and the fault is on this side.

## Doing it by hand on Windows

The same thing, with no switch and nothing to install. It is two steps and it is what
**Only here** was going to do for you.

First, find an output nobody is listening to. Most machines already have one and it does
not need anything plugged into it: a digital output with no cable in it, an HDMI socket
with no screen on it, or a virtual output something else installed. Any output can be
captured whether or not there is a speaker on the end of it.

Then, in Windows: Settings, System, Sound, Volume mixer. Find the program, open it, and
set **Output device** to that unused output. It stops coming out of your speakers the
moment you do.

Last, back here: set the IN strip **Source** to that same output, which the picker offers
as one of the outputs it can record. Throw **Hear it** to bring it through the recording
chain and out of the master.

The program is now heard through JingleBox2 and nowhere else, which is what was wanted.
Windows did the moving, so it is Windows that puts it back: set Output device to Default
in the same place when you are done.

**A virtual cable is only needed where there is no spare output.** VB-CABLE is the usual
one. It is a zip rather than an installer to double click: inside is
`VBCABLE_Setup_x64.exe`, it has to be run as administrator or it does nothing whatever
and says nothing about it, and it wants a restart afterwards. Then CABLE Input is the
output to send the program to, and CABLE Output is what to record.

**The source is picked on the mixer**, at the foot of the IN strip, because that is
the strip it is about. RECORD says what it is set to and does not set it, since one
choice offered in two places is two ways of doing one thing.
