# Control surfaces

What each piece of hardware is allowed to do.

A controller often shows up as several ports and only one of them carries its knobs
and keys. Tick what a device is allowed to do once, here, and the job is put on
whichever port it really uses for that: a MiniLab shows up four times and only one
of them is the keyboard.

- **Pads** lets it fire the pads on FIRE.
- **Tracker** lets it play notes into the pattern.
- **Controls** lets its knobs and buttons drive things you have pointed them at.
- **Transport** lets its play, stop and record keys work the deck.

A pad box and a keyboard can be connected at the same time: one fires pads while the
other plays the tracker.

Nothing here needs a file. A controller nobody has written anything about works the
moment it is plugged in, and a profile only adds names, the shape of a control, and
which port does what. Where there is one, a control is called `Encoder 3` rather than
`CC 89`.

## Pointing a knob at something

There is no table of controller numbers to fill in. Press `Ctrl+Shift+M`, rest the
pointer on the control you want driven, and touch the knob or button on the desk: the
link is made from what you were pointing at. The key turns the mode over rather than
being held down, so press it again when you are done, or the next thing you touch is
learned rather than played. It works on a machine's face, on an
effect's, on a mixer strip, on the transport, and on a pad on FIRE.

A link says which machine and which control, never which track or which song, so one
knob is OddSkilla's filter wherever OddSkilla is playing. It also remembers the
controller it was learned on, so two desks pointed at one machine do not fight.

What each controller is pointed at is on MIDI CC, the last word along the top, one
card per controller per thing. A card is a template: it can be written out to a file
and read back on another machine.

## The pitch and modulation wheels

They need none of that. Controller one is the modulation wheel in the MIDI
specification itself and the pitch wheel has a message of its own, so both work the
moment a keyboard is plugged in and its port is ticked for **Tracker**. There is
nothing to point, nothing to learn and nothing stored.

A wheel goes where the keys beside it go: the same port, the same half of the
application, and the same track. A keyboard whose MIDI in is pointed at track three
bends track three; otherwise the wheels reach whatever the cursor is on, or the rack
while you are building a sound there.

How far the pitch wheel bends is the instrument's own setting, two semitones unless
you say otherwise. What the modulation wheel turns is the machine's: OddSkilla gives
it the vibrato depth, Ouroboros the modulation amount, Operetta the feedback, and a
machine that names none is left alone by it. A plugin is sent both wheels as they
arrived and decides for itself, since the range and the destination are settings on
its own face.

You can still point the modulation wheel at something with `Ctrl+Shift+M`, and that
link then wins: it stops being a wheel and becomes a knob like any other. The pitch
wheel cannot be pointed at anything, because it sends no controller number for a link
to name.

Both are drawn beside the keyboard on an instrument's panel, and they only report.
They move when the hardware moves and there is nothing on them to drag: a wheel on
the screen that could be dragged would disagree with the one under your hand the
moment you touched it.
