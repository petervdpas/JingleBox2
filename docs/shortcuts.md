# Shortcuts

Built: the module, Save and Delete where they mean something, and undo in the two places that
have a document. Not built: the settings page to edit the keys.

## The shape

Three pieces, deliberately apart.

```
ShortcutAction     the closed list of things a key can ask for
ShortcutMap        which key asks for which. a setting: stored, edited, shown
ShortcutKeys       delivery. knows nothing about what a key is or what an action means
IShortcutContext   what a page can do, answered by the page
```

The split is the whole point. A page that edits shortcuts binds to the map and nothing else; it
never has to know how a keystroke reaches anybody. And the dispatcher never has to know what any
page can do.

## Context, without a register of contexts

The dispatcher starts at whatever has the keyboard and walks outwards. The first thing that says
it can do the action does it; if nothing says so, the key carries on as though none of this were
here.

Which means nothing has to be told when the context changes. A dialog answers because the dialog
is where the focus is, and closing it changes the answer by itself. The same shape
`TransportSwitch` already uses for play and record: the page you are on owns the keys, and a page
with nothing to play does nothing with them.

Two consequences worth knowing:

**Saying no is the common answer and is not a failure.** Ctrl+S on the pads reaches nobody and
does nothing, which is right. A page that wants it says so.

**A control can answer for itself.** Saving a machine that has never had a folder must ask where
to put it, and asking is a window's job, so `MachineEditorView` answers rather than its view
model. A view model answers where that is simpler.

## What answers what today

```
TRACKER              Save     the song, through TrackerViewModel.SaveCommand
                     Undo     the last thing you did in the tracker, of either kind
                     Redo     the same, forwards
machine editor       Save     the machine, and asks for a folder when it has none
                     Delete   the element selected on the panel being laid out
                     Undo     the machine, as its own file would hold it
                     Redo     the same, forwards
everywhere else      nothing
```

Saving is answered by the page and undo by the grid, on purpose. The keystroke walks outwards
until something takes it, so Ctrl+S passes the tracker's own view model and reaches
`MainViewModel`, and Ctrl+Z stops at the grid, which is the only thing that has a history.

## The keys

```
Save    Ctrl+S
Delete  Ctrl+D
Undo    Ctrl+Z
Redo    Ctrl+Shift+Z
```

Stored in the settings, and only what somebody changed: a shortcut left alone is not written
down, so a default that turns out to be a poor choice can be improved and will reach anybody who
never had an opinion about it. A shortcut deliberately taken off is stored with no keys, which is
how that is told from never having been touched.

One key does one job. Putting an action on a key takes that key off whatever else had it, because
two actions on one keystroke is a state a settings page should never be able to leave somebody in:
only one of them could ever happen, and which one would be an accident of the order they were
stored in.

Undo and redo are left alone while a caret is blinking, because in a text box that keystroke has
only ever meant the box's own undo. Save is not like that and is taken wherever it is pressed.

## Undo, in the tracker

Two kinds of step, one history, because Ctrl+Z means the last thing you did and not the last
thing you did of a particular kind. Keeping them in two histories would give one keystroke two
meanings and a person no way of knowing which they were about to get.

```
a pattern      typing, clearing, transposing. a step is the pattern's cells: a memory copy of
               a few kilobytes, 0.15ms for the largest pattern that can exist
the song       an instrument added or taken out, the order, how many tracks. a step is the
               song as its own file would hold it, 12 to 82 KB
```

They are kept apart because they cost very differently. Serialising the whole song for every
keystroke would work and would be wasteful in exactly the place that must not be.

### The pattern half

Built. `Tracker/TrackerHistory.cs`.

Whole copies of the pattern rather than a description of each change, which is the right trade
here and would not be everywhere. A pattern is one array of value types with no allocation per
cell, so a step is a memory copy: measured at 0.15ms for the largest pattern that can exist, two
hundred and fifty six lines by thirty two tracks, and a few kilobytes for one of the usual size.
Describing an edit instead would mean a type per operation, an inverse for each, and the
certainty that the nineteenth one written would forget its inverse and undo would quietly corrupt
a song. A copy cannot be wrong about what it holds.

The unit is one call to `PatternEdit`, which is why that class being the only door matters. The
hook is there rather than at the call sites, so an edit added later is recorded without anybody
remembering to say so. Paste is the one edit that lives elsewhere and is hooked by hand.

Three things worth knowing about how it behaves:

**An edit that changed nothing leaves no step.** Worked out by noticing that the pattern still
holds exactly what the last step kept, so a key that did nothing does not have to be undone.

**Every step remembers which pattern it belongs to.** Undo after switching patterns goes back to
the right one and takes the view with it, rather than changing a pattern behind your back while
you look at another.

**It is emptied when a song is opened.** A history outliving its song would hand somebody another
song's notes.

Bounded two ways, a hundred steps and thirty two megabytes, so an enormous pattern keeps fewer
steps rather than all of them.

### The song half

For the edits a pattern snapshot cannot describe. Taking an instrument out renumbers every
pattern that referred to it, which is an edit across the whole document and exactly the sort of
thing done by accident.

A step goes through `SongStore.Copy` and `Uncopy`, which are the same reader and writer a save
goes through. Those two already know what belongs in a song and what does not, so a step cannot
disagree with what saving would produce, and a second copier written beside them is how the two
drift apart.

It comes back through `Song.TakeFrom`, which pours the contents in without the song becoming a
different object, because the player, the mixer, every panel and the view model all hold the one
they were opened on.

**And the patterns keep their identity too, which cost a bug to find.** The cheap steps hold a
pattern by reference. Replacing the pattern list on the way back left every one of them pointing
at an object no longer in the song, so undoing a note after undoing an instrument appeared to do
nothing at all. A pattern that already exists is filled rather than swapped, and only the count
changing adds or drops one.

## Undo, in the designer

Built. `ViewModels/DesignHistory.cs`.

The same principle as the tracker's and a different mechanism, because the document is a
different shape. A pattern is one array of value types and a step is a memory copy. A machine is
a tree of elements, a list of parameters and a dozen fields beside them, and copying that by hand
means a clone that is right the day it is written and wrong the first time somebody adds a field.

So a step is the machine as its own file would hold it. That is not a trick. `machine.json` is
exactly the document being edited, its reader and writer already exist and are already trusted
with people's work, and a step written the same way cannot disagree with what a save would
produce. Fourteen kilobytes for a real machine, so a hundred steps is under two megabytes.

Three things worth knowing:

**Put back in place, not as a new object.** Panels, the rack and the utilities all hold the
project they were opened on, and handing the editor a different instance would leave every one of
them pointed at the machine as it was before the undo. The fields go back into the project that
is open, and the editor hangs its wrappers off the tree again.

**Every field the file carries, found rather than listed.** The restore walks the project's own
serialisable properties. A list written out by hand would be right on the day and wrong the first
time a field is added, and the way that fails is the worst kind: an undo that silently drops
whatever was forgotten. Tested by giving a machine a summary, an author and a theme, undoing, and
finding all three back without any of them being named anywhere in the history.

**A machine's own fields had to be told to speak.** The name, what it is, who made it and its
version bind straight through to `MachineProject`, which is a plain object with nothing to say
when it moves, so a rename reached nothing: the Save button stayed cold and undo could not take it
back, while dropping a knob on the panel did both. The boxes now tell the editor on losing focus,
which is also the right unit: a name typed in is one edit and not eleven. The colour had the same
hole, and `Dressed` was saying what it did to the two things showing the colour and to nothing
else.

**The door is a redraw rather than an edit.** Every edit in the designer ends at
`MachineEditorViewModel.Redraw`, so that is where the history hears about it. It is told more
often than there are edits, which is safe: a redraw where nothing about the machine moved reads
the same as before and leaves no step. Over-telling costs a comparison; under-telling would be an
edit that cannot be undone.

## Undo everywhere else, which does not exist

There is no undo anywhere in this application. Not a stack, not a command history, not a single
type with the word in it. So Ctrl+Z is a key that is delivered correctly to a page that correctly
says it cannot, which is the honest state to be in and is not the state anybody wants.

It is not one feature. Each context that wants it needs its own history, and they are different
enough that a shared one would be a lie:

```
a machine's      turning knobs, in a song. the unit is unclear: one knob move is not worth a
settings         step and a hundred of them are, so it wants gathering by time or by which knob
the pads         what a pad is pointed at
```

The knobs are the interesting one left, and the hard part is not the history. It is the
gathering: a parameter driven by a controller or by automation produces a step per message, which
is a history nobody can walk. "The same control, within a moment" is the usual answer, and it is
the same shape as the coalescing already on the drawing thread.

Two things that are true whatever is built:

**It belongs to the context, not to the application.** Undo on the tracker should walk the
tracker's edits and nothing else, and the shortcut layer above already delivers to whoever claims
it, so a context that grows a history needs no change here at all.

**A history has to know what to gather.** Anything driven by a knob, a controller or automation
produces a continuous stream, and a step per message is a history nobody can walk. Gathering by
"the same control, within a moment" is the usual answer and is the same problem the coalescing on
the drawing thread already solves for a different reason.

## Still open

- The settings page. `ShortcutActions.Everything` exists so a page can build itself from it, the
  way the log's areas page does, and nothing has been built yet.
- Which pages should answer Delete. RECORD has takes and PADS has pads, and both would be
  destructive without asking, which is a decision rather than a wiring job.
- Whether a shortcut should be shown anywhere it applies, in a tooltip or beside a button. A key
  nobody knows about is a key nobody presses.
