# Soundmachines and effects

The two things laid out here, and the one way they differ.

**A soundmachine is played.** It is sent notes and it sounds them, and what you are laying out is
its face: the knobs, the keyboard, the pads, whatever it wants shown. In a song it is the
instrument a track plays.

**An effect is not played.** It is handed a whole track's audio and hands it back changed, so it
has no keyboard, no zones and no pads. In a song it is a slot on a track's chain, and the same
effect on two tracks is two sets of knob positions.

**That is the only difference.** What a song does with it. Everything before that step is one
thing done twice: made here, registered in SETTINGS, System, put on the rack, drawn from one
library, carrying presets and a help page, travelling as a zip, and taking a knob pointed at it
the same way.

## The engine

Both are a face over an **engine**, and the engine is compiled into the application rather than
living in the folder you are making. The face is yours: its name, its id, its colours, its knobs
and what each one is called.

So the manifest says which engine it wants, and **that is the only thing this build has to
recognise**. The id is yours and can be anything, any number of devices can name one engine, and
two kits you lay out here are two devices rather than one replacing the other.

A device asking for an engine this build has not got is read off disc and left there rather than
put on the rack, which is what makes a folder from a later version harmless.

## Two tabs, two pieces of work

Each keeps its own project, its own undo and its own unsaved changes, so moving between them
loses nothing. New follows whichever tab you are on: Machines writes `machine.json` and Effects
writes `effect.json`. A folder is one or the other, never both.
