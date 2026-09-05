# Laying out a face

Dropping parts on a device and saying what each one turns.

The page has three columns: the parts you can add on the left, the face itself in the
middle, and what the picked part is set to on the right. Everything is dropped rather
than typed, and nothing is ever added to a face behind your back: **what a face does
not draw, nobody draws**. A soundmachine with no keyboard shows no keyboard, and one with
no name badge shows no name.

## The pages under the header

**Screen** is the face. **Presets** is what the device starts you from, and how much room each one
leaves. **Helptext** is the page the device carries about itself: what it is, what its controls do,
and anything somebody opening it for the first time would have to guess. It is written in
markdown, saved as `help.md` beside the manifest, and it travels in the zip, so whoever you hand
the device to gets the page with it. A device with nothing written there has no Help line on its
Menu, which is the honest state rather than a line that opens an empty window.

## The two things a part needs

A part is a control, and a control turns a parameter. So there are two lists and they
have to agree: **Parameters** is what the device can be set to, and each one is a key, a
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

One menu to a device, and it is a part like any other: drop it, pick which corner, and
tick which lines it carries. The two lower corners are not offered, because a panel
taller than its window scrolls and a button below the fold is one nobody can find.

A badge showing the instrument's name is a part too. A machine that does not carry one
shows no name, which is a machine saying its face is its own; nothing is lost, since
the window is titled with it and the song's instrument list renames it.

## Undo, and saving

Every edit leaves a step and `Ctrl+Z` takes it back, including moving a part, renaming
a parameter and changing the grid. An edit that changed nothing leaves no step.

**Save** writes the manifest where the device lives. **Save as...** carries the whole
folder somewhere else, pictures, presets and sounds included, and works on that copy
from then on: it is how an edited device is put back over the copy that ships beside the
program. The id does not change, because a copy of a device is that device somewhere else
and not a new one. **New** is how you make a different one.

The id is yours and can be anything. What this build has to recognise is the **engine**
the manifest names, since that is what makes or works on the sound and it is compiled
into the application rather than living in the folder. Any number of devices can name
one engine, so two kits you lay out here are two devices.

**Give your own device its own id, and never one that ships.** A device is known by its
id and by nothing else, so a device of yours carrying a shipped id *is* that device as
far as the application is concerned: on the next start it is brought up to date from the
copy beside the program, and an afternoon's work opens as the device that ships. That is
deliberate, since it is how a corrected device reaches anybody at all, and there is
nothing in a folder that could say who wrote what is in it.

Under an id of its own, nothing ever touches it. The start-up pass only walks the files
that ship, so your device, your presets and anything else you put in its folder are not
looked at, let alone removed.

A device naming an engine this build has not got is read off disc and left there rather
than put on the rack, which is what makes a folder from a later version harmless. See
"Soundmachines and effects" for what an engine, a soundmachine and an effect each are.
