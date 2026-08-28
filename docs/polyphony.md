# Polyphony

Not built. This is the plan, written down while the reading was fresh, so whoever picks it up
starts from what the code already does rather than from the beginning.

## Two features share the word

**Note columns** are chords. A track is given a second and a third note column, each one a
monophonic voice of its own, all of them sharing the track's instrument, its mixer strip, its
inserts and its ducking. Typing C, E, G on one row fills columns 1, 2 and 3. This is what
Renoise means by polyphony and what a tracker with only one note per track cannot do at all.

**New note action** is tails. It answers a different question: when a note arrives where one is
still sounding, what happens to the old voice? Cut it, let it release, or leave it alone. With
release, a piano part in a single column overlaps by itself, because the previous note is still
decaying while the next one starts.

They are orthogonal and they cost wildly different amounts here. One is a setting and a method
that already exists; the other is the pattern editor. Take them in that order.

## What a track is today

One note, and the code says so in three places.

```
Pattern             TrackerCell[line * TrackCount + track]      one cell per track per line
TrackMixer.NoteOn   Cut(track) before the new voice is added    "one voice per track"
PluginNoteOn        instrument.AllNotesOff() before NoteOn      "one note a track"
```

The audio side has nothing to learn, which is the part worth knowing before estimating any of
this. Auditions are already polyphonic through the same mixer: a voice played by hand carries
`SynthVoice.NoTrack` and an `Audition` id instead of a track, and they pile up until the
instrument says `OneVoice`. Voices already carry an owner, tracks already render on their own
bus, and 48 of them already sum. What is monophonic here is the pattern and the cut, not the
engine.

## New note action, which is nearly built

`IVoice` already has both endings, and they are different methods:

```
SynthVoice.Cut()       NoteOff(CutSeconds)   a 4ms fade, what a new note does today
SynthVoice.NoteOff()   the patch's release   what a pattern's OFF does
```

So the action is a choice between two calls that both exist, plus the third case of making
neither. Renoise offers exactly three (`NEW_NOTE_ACTION_NOTE_CUT`, `NOTE_OFF`, `SUSTAIN`, from
`renoise/song/instrument/sample.lua`), having dropped Impulse Tracker's fourth, Fade, which
needs a fadeout rate no patch here has. Three is the right number.

Where it goes: on `TrackerInstrument`, beside `OneVoice`, because it is a fact about the sound
and not about the track. `RecordingPatch` and `RecordingValues` already show how a flag is
stored and offered as a machine parameter. Cut stays the default, so nothing anybody has
already made changes.

Two things need building rather than choosing.

**Per-note offs for plugins.** `PluginNoteOn` calls `AllNotesOff()` before every note, which is
correct for one voice and wrong for anything else. `IPluginInstrument.NoteOff(int semitone)`
already exists; the mixer has to remember which note each track is holding, and later which
note each column is. This is the only real work in this half, and it is also the piece note
columns need, which is the second reason to do this first.

**Duplicate check.** Without it a fast run in release mode reaches `MaxVoices` and steals.
Stealing already works and takes the oldest first, so the failure is graceful rather than a
crash, but Impulse Tracker's rule is worth having: the same note arriving on the same track
cuts the previous instance of that note. A few lines inside `Cut`.

## Note columns, which is the editor's work

What a Renoise note column carries is the whole cell again: note, instrument, volume, panning,
delay and its own effect (`renoise/song/pattern/line.lua`). Here that means a column is another
`TrackerCell`, unchanged. Nothing about the cell type has to move.

How many, and where the count lives. Renoise: `max_note_columns` is 12, `visible_note_columns`
is 1 to 12, and it sits on `renoise.Track`, which is the song, not the pattern. Take that as it
stands. A part is played on so many voices whatever pattern it is in, and a count that varied
per pattern would make copying a track between patterns a question with no good answer. One to
eight here, default one, so nothing widens until it is asked for.

The pieces, in the order they stop being invisible.

**The pattern.** One flat array of value types is right and stays. The stride becomes the row's
total column count rather than `TrackCount`, and it changes when a track's count does.
`Pattern.Rebuild` already keeps whatever still fits when the shape changes, and this is the same
operation with one more axis.

**The file.** `SongStore.PatternDocument.Cells` is a list of `"line:track:cell"` strings, one
per used cell. It becomes `"line:track:column:cell"`, and a three-part entry read back means
column 0. Old songs load untouched, no migration and no version flag, which is what that format
was chosen for. Worth a test of its own.

**The sequencer.** `EventsFor` walks tracks and gains an inner walk over columns. `TrackerEvent`
names a track and must name a column too, or `Stop(track)` from one column's OFF kills the
whole chord.

**The mixer.** `Cut(track)` becomes cut by track and column, and `SynthVoice` carries the column
beside its track. `SamplePosition(track)` returns the first voice it finds on a track and will
have to say which one it means, since a panel's playhead cannot follow three at once.

**The cursor.** `PatternCursor.MoveColumn` flattens a track into a fixed four columns; it has to
flatten a variable number. `PatternMetrics.TrackWidth` becomes per track, and `TrackAt` and
`ColumnAt` stop being a division and become a walk. All of it is pure arithmetic with tests
already sitting in `Tests/PatternTests.cs`, so it is checkable without a window. The grid is
custom drawn, so a variable track width is arithmetic and not layout.

**The selection.** `PatternSelection` holds two corners in lines and tracks. Corners become
lines and flat columns. Pasting a block into a track with fewer columns pastes what fits, the
same rule `Rebuild` uses.

**Entry.** Renoise's rule is that a note typed while another key is held goes to the next column
of the same track, so a chord played on a keyboard or typed on the letter rows lands across
1, 2, 3. `MidiNoteInput` is pure and stays that way; held-note counting belongs in the view
model.

**History.** `Pattern.Cells`, `Holds` and `Restore` carry lines and track count and will carry
the column counts. `TrackerHistory` compares shape before restoring, so if it is not taught the
new axis an undo across a column count change does nothing and says nothing. This codebase has
had that exact bug twice, in `currentPattern` and in `TakeFrom`, and both times it survived
because doing nothing looks like working.

## Effort

```
new note action, three actions                    half a day
per-note plugin offs, and the note bookkeeping    a day, and note columns need it too
column axis: pattern, file, sequencer, mixer      two days
cursor, metrics, selection, entry, history        three to four days, all of it interface
```

## Decided already

- New note action first. It is a day, it is worth having on its own, and it builds the per-note
  plugin bookkeeping that note columns cannot do without.
- Three actions, cut, release and sustain, cut being the default. Renoise's set, for Renoise's
  reason: fade needs a rate no patch here has.
- The action belongs to the instrument, beside `OneVoice`, not to the track.
- A note column is a whole `TrackerCell`. No new cell type.
- The column count belongs to the song's track, not to the pattern.
- The file grows a fourth field and old songs read as column 0.

## Still open

- Whether the instrument memory in `TrackerSequencer` is per track or per column. A blank
  instrument column means the last one played, and the columns of a track are usually one part
  on one instrument, which argues for per track. Renoise gives every column its own instrument
  value, which argues the other way. The volume is not in doubt: gain has to be per column, or
  one voice of a chord would set the level of the others.
- Whether `MaxVoices`, 48, still holds. Eight tracks of four-note chords in release is over it,
  and stealing the oldest during a sustained chord is audible in a way that stealing during a
  monophonic part is not.
- Column mutes and column names, which Renoise has. Cheap once the axis exists, and no use at
  all until somebody has written a chord with them.
- Whether a track's insert chain and a future automation lane stay per track. They should:
  columns share a bus, which is the whole point of them being one track.
