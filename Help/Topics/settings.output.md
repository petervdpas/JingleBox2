# Output

Where the sound comes out.

Everything the application plays goes out of the device picked here: the pads, the
tracker, and whatever the plugins are playing. The choice is remembered.

The list says what each entry is rather than only naming it, because they are not all
the same kind of thing. On Windows the system's own outputs are listed alongside any
ASIO drivers installed, and an ASIO driver is not a device in the same sense: it owns
the card, so picking one takes a different path through the application entirely.

Changing the device closes the audio and opens it again, so it can be chosen by
listening. Everything that was playing stops.

An empty ASIO list says which of two things is true: that ASIO is a Windows standard
and there is none on this system, or that ASIO is here and no driver is installed,
which is most Windows machines until a card's own driver or something like ASIO4ALL
puts one there.

Picking an ASIO driver needs one thing next door. A driver owns the card and can be handed one
stream, so everything this application plays has to be summed before it leaves, which is the
"sum everything onto one bus" tick under Engine. With it on, the tracker, the pads and a take
being auditioned all go out of the driver. With it off, the tracker is heard and the pads and
RECORD are silent, and nothing on this page would tell you why.

The rate, the buffer and how the sound is kept fed are next door, under Engine.
