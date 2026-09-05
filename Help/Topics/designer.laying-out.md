# Laying out a face

Dropping parts on a box and saying what each one turns.

The page has three columns: the parts you can add on the left, the face itself in the
middle, and what the picked part is set to on the right. Everything is dropped rather
than typed, and nothing is ever added to a face behind your back: **what a face does
not draw, nobody draws**. A machine with no keyboard shows no keyboard, and a box with
no name badge shows no name.

## The pages under the header

**Screen** is the face. **Presets** is what the box starts you from, and how much room each one
leaves. **Helptext** is the page the box carries about itself: what it is, what its controls do,
and anything somebody opening it for the first time would have to guess. It is written in
markdown, saved as `help.md` beside the manifest, and it travels in the zip, so whoever you hand
the box to gets the page with it. A device with nothing written there has no Help line on its
Menu, which is the honest state rather than a line that opens an empty window.

## The two things a part needs

A part is a control, and a control turns a parameter. So there are two lists and they
have to agree: **Parameters** is what the box can be set to, and each one is a key, a
name, a unit and its ends; the parts on the face name one of those keys.

The key is what travels. It is what a song writes down, what a preset holds, and what
a knob on your hardware is pointed at, so it outlives the name on the front and is
never changed once anything has been saved with it. The name is what a person reads
and is yours to edit whenever you like.

## Properties, and why a part can look wrong

Each part carries properties, added with **+ Property**, and they are words rather
than a form: `dial` for how big a knob is drawn, `span` for how many columns it takes,
`gap`, `cell` and `columns` on the strip that holds them, `caption` on a group,
`corner` on a menu, `tip` for what it says when the pointer rests on it.

A property whose name the drawing does not know is ignored in silence. That is right,
since a face written by a later version has to open here at all, but it is also how a
face comes to look cramped for no visible reason: the part is asking for something
nobody is listening to. If a control is drawing at a size you did not ask for, the
first thing to check is the spelling.

## The face is the machine's

One menu to a box, and it is a part like any other: drop it, pick which corner, and
tick which lines it carries. The two lower corners are not offered, because a panel
taller than its window scrolls and a button below the fold is one nobody can find.

A badge showing the instrument's name is a part too. A machine that does not carry one
shows no name, which is a machine saying its face is its own; nothing is lost, since
the window is titled with it and the song's instrument list renames it.

## Undo, and saving

Every edit leaves a step and `Ctrl+Z` takes it back, including moving a part, renaming
a parameter and changing the grid. An edit that changed nothing leaves no step.

**Save** writes the manifest where the box lives. **Save as...** carries the whole
folder somewhere else, pictures, presets and sounds included, and works on that copy
from then on: it is how an edited box is put back over the copy that ships beside the
program. The id does not change, because a copy of a box is that box somewhere else
and not a new one. **New** is how you make a different one.

A box you make under a new id is read off disc and never reaches the rack, and that is
not a fault. The engine that makes the sound is in the application rather than in the
folder, and an id it has no engine for is passed over. See "Machines and effects" for
what an engine, a machine and an effect each are.
