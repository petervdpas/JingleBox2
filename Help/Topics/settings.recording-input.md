# Recording input

Where the RECORD tab captures from.

Pick the device the RECORD tab captures from. The choice is remembered between
sessions.

On Linux this can go further than a device: the source picker offers the programs
that are playing, so a browser can be recorded on its own. That is PipeWire, which
treats every stream as something that can be patched.

On Windows the same picker offers each output through WASAPI loopback, which records
everything that output is playing rather than one program.

**The source is picked on the mixer**, at the foot of the IN strip, because that is
the strip it is about. RECORD says what it is set to and does not set it, since one
choice offered in two places is two ways of doing one thing.
