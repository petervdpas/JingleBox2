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

There is no table of controller numbers to fill in. Rest the pointer on the control
you want driven, hold `Ctrl+Shift+M`, and touch the knob or button on the desk: the
link is made from what you were pointing at. It works on a machine's face, on an
effect's, on a mixer strip, on the transport, and on a pad on FIRE.

A link says which machine and which control, never which track or which song, so one
knob is OddSkilla's filter wherever OddSkilla is playing. It also remembers the
controller it was learned on, so two desks pointed at one machine do not fight.

What each controller is pointed at is on MIDI CC, the last word along the top, one
card per controller per thing. A card is a template: it can be written out to a file
and read back on another machine.
