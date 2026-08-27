# Shortcuts

Built: the module, and Save and Delete where they mean something.
Not built: undo and redo, which have nothing to walk, and the settings page to edit the keys.

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
machine editor       Save     the machine, and asks for a folder when it has none
                     Delete   the element selected on the panel being laid out
everywhere else      nothing
```

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

## Undo, which does not exist

There is no undo anywhere in this application. Not a stack, not a command history, not a single
type with the word in it. So Ctrl+Z is a key that is delivered correctly to a page that correctly
says it cannot, which is the honest state to be in and is not the state anybody wants.

It is not one feature. Each context that wants it needs its own history, and they are different
enough that a shared one would be a lie:

```
the tracker      typing notes into a pattern. many small edits, one per keystroke, and the
                 obvious unit is a keystroke. the pattern is small and could be copied whole
the designer     laying out a machine's panel. the unit is a drag, a resize, an element added
                 or taken off, and the document is a tree
a machine's      turning knobs. the unit is unclear: one knob move is not worth a step and a
settings         hundred of them are, so it wants gathering by time or by which knob
the pads         what a pad is pointed at
```

The cheapest thing that would be genuinely useful is the tracker's, because a pattern is small
enough to keep whole copies of and the edits are already discrete. The designer's is the one
somebody would miss most, and the hardest.

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
