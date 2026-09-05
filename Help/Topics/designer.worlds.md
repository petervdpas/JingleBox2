# Machines and effects

The two things laid out here, and how they differ.

A machine is played. It is sent notes and it sounds them, and what you are laying out
is its face: the knobs, the keyboard, the pads, whatever the machine wants shown. It
goes on the rack and a song points a track at it.

An effect is not played. It is handed a whole track's audio and hands it back changed,
so it has no keyboard, no zones and no pads. It goes on a track's chain, under the
pattern, and the same effect on two tracks is two sets of knob positions.

Both are a face over an engine, and the engine is in the application rather than in
the folder you are making. That is why New gives an id that is yours: this build has
no engine behind it, so it is read off disc and never reaches the rack. Ours are the
ones whose ids the application knows.

The two tabs are two pieces of work. Each keeps its own project, its own undo and its
own unsaved changes, so moving between them loses nothing.

What the two share is everything else. Both are laid out here, both carry presets,
both carry their own help page, written on the Helptext tab and read from the box's own
Menu, and both travel as a zip that the other end reads under SETTINGS, System. New
follows whichever tab you are on.
