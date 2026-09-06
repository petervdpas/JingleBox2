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
that is chosen, and a virtual cable is the usual answer. A spare socket nobody has
speakers on does the same job. Until one is picked the switch stays grey.

What that sets is the same per-program output Windows keeps in Settings, Sound, Volume
mixer, so it can be seen and undone there as well. It is put back when you pick another
source or close the application.

**The source is picked on the mixer**, at the foot of the IN strip, because that is
the strip it is about. RECORD says what it is set to and does not set it, since one
choice offered in two places is two ways of doing one thing.
