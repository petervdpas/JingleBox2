# The pads

What a pad plays, and what happens when you hit it.

PADS is where they are set up and FIRE is where they are played. The grid on both is
the same shape and the same reach, so a pad is in the same place under your hand
whichever page you are on.

Pick a pad in the grid to edit it. What it holds is a name, a colour, a level, a fade
in and out, and a source.

## What a pad plays

A pad plays one of two things.

A **recording** off the shelf, picked from the takes RECORD has made. It is picked
rather than typed, so the application owns every file a pad depends on and a pad
cannot end up pointing at something that was moved.

A **stream**, which is an address beginning `https://`. Nothing is downloaded and
nothing is kept: it plays for as long as it is playing.

**Loop** makes it start again at the end rather than stopping, which is what a bed or
an atmosphere wants.

## Hitting one

Toggle mode is in SETTINGS, under Control Surfaces, and it decides what a second hit
means: on, a pad keeps playing until it is hit again; off, it plays for as long as
the pad is held. It is the same answer for the mouse and for a pad box.

A playing pad keeps its own colour and walks through the ones either side of it, so a
bank says what is going without being read. That is **Pulse while playing**, beside
toggle mode, and it is the one thing on FIRE that draws while nothing has changed:
switched off, a playing pad is still told apart by its ring and its meters.

## Profiles

The picker at the top of PADS is a whole bank under a name. Add one, lay it out, and
switch between them: a set for the breakfast show and a set for the football is two
profiles rather than two applications.

## Pointing hardware at one

There is no table of notes to fill in. Rest the pointer on a pad on FIRE, hold
`Ctrl+Shift+M`, and hit the pad on your box. It lands on the same layer every other
link does and turns up on MIDI CC under a card headed Pads.

A fresh installation has nothing pointed at the pads, deliberately: a pad nobody has
pointed at should do nothing rather than something surprising.

Undo works on both pages, and a step is every pad at once, since how many pads there
are is an edit too and it is about none of them in particular.
