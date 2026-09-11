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

## Choosing a source takes it off its own output

Capturing a source leaves it playing wherever it was playing, which is what every program
that records does. Here it does not. On air that is wrong: what is going out must not also
be coming out of the desk speakers a moment later. So a source pointed at the input belongs
to the desk from that moment, and the desk is the only way back to a speaker.

That means a browser goes quiet the moment you pick it. **Hear it**, under the picker, is
what brings it back: thrown, what is coming in goes through the Recording Effects chain and
out of the master. Off, the input is captured and nobody hears it.

Picking another source puts the last one back, and so does closing the application.

**On Windows a source needs somewhere to go**, since there is no link to unplug: a program
can only be pointed at another output. **Send it to**, under Hear it, is where that is
chosen, and it is only drawn on a machine that needs it.

**And on Windows taking a source aside does not work.** It reaches per-program output
through an interface of Windows own that this application cannot call: the marshalling it
needs is not in .NET. The log says so on every start. It is not your machine and it is not
a setting: the interface is there and answers, and the fault is on this side. The next
section is how to do the same thing by hand.

## Doing it by hand on Windows

The same thing, with nothing to install. It is two steps, and it is what this application
does for you on Linux and cannot yet do for you here.

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
