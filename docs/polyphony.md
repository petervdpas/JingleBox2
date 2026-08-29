# Polyphony

Built, both halves. This is what was decided, how it was done, and the one piece that was
deliberately left.

## Two features share the word

**Note columns** are chords. A track is given a second and a third note column, each one a
monophonic voice of its own, all of them sharing the track's instrument, its mixer strip, its
inserts and its ducking. Typing C, E, G on one row fills columns 1, 2 and 3. This is what
Renoise means by polyphony and what a tracker with only one note per track cannot do at all.

**New note action** is tails. It answers a different question: when a note arrives where one is
still sounding, what happens to the old voice? Cut it, let it release, or leave it alone. With
release, a piano part in a single column overlaps by itself, because the previous note is still
decaying while the next one starts.

They are orthogonal and they cost wildly different amounts here. One was a setting and two
methods that already existed; the other was the pattern editor. They were taken in that order,
and doing the first one first was what paid: the per-note plugin bookkeeping it forced is the
thing note columns could not have been built without.

## What a track is

One note, in one column, and three places said so:

```
Pattern             TrackerCell[line * TrackCount + track]      one cell per track per line
TrackMixer.NoteOn   Cut(track) before the new voice is added    "one voice per track"
PluginNoteOn        instrument.AllNotesOff() before NoteOn      "one note a track"
```

The second and third have moved. `MakeWay` is what makes room now and it does what the
instrument asks; `PluginNoteOn` sends a note off for the note it remembers rather than all
notes off. The first is the pattern and is the whole of what note columns are.

The audio side had nothing to learn, which is the part worth knowing before estimating any of
this. Auditions were already polyphonic through the same mixer: a voice played by hand carries
`SynthVoice.NoTrack` and an `Audition` id instead of a track, and they pile up until the
instrument says `OneVoice`. Voices already carried an owner, tracks already rendered on their
own bus, and 48 of them already summed. What was monophonic here was the pattern and the cut,
not the engine.

## New note action, which is built

`Tracker/Enums/VoiceEnding.cs`: cut, release, sustain, on `TrackerInstrument.NewNoteAction`
beside `OneVoice`, cut being the default so nothing anybody had already made changed. Renoise
offers exactly the same three (`NEW_NOTE_ACTION_NOTE_CUT`, `NOTE_OFF`, `SUSTAIN`, from
`renoise/song/instrument/sample.lua`), having dropped Impulse Tracker's fourth, Fade, which
needs a fadeout rate no patch here has.

`IVoice` already had both endings and they were already different methods, so the voice half
was a choice between two calls that both existed:

```
SynthVoice.Cut()       NoteOff(CutSeconds)   a 4ms fade, what a new note did everywhere
SynthVoice.NoteOff()   the patch's release   what a pattern's OFF does
```

`TrackMixer.MakeWay` is that choice, held under the mixer's lock because what it decides about
has to still be true when the new voice is added. The same note arriving where it is already
sounding is cut under all three: two copies of one note are a retrigger everywhere in music,
and letting them pile up is how a sustaining part walks into `MaxVoices` and starts stealing.

**Per-note offs for plugins** was the real work, as expected, and it is also the piece note
columns cannot do without. A plugin cannot be asked what it is holding, so the host remembers
what it said: `HeldNotes` is that record, one per track and one for the audition slot, bounded
at sixteen and stealing its oldest when it is full. Every method that lets go writes the notes
out to the caller rather than ending them itself, because the mixer holds a lock while it
decides and may not hold one while it talks to a plugin. Where nothing is remembered the whole
plugin is still asked to let go, which is what that path always did and costs one message on
the first note after a stop.

Two things fell out of that record and were taken:

- A note played by hand on a plugin now piles up like every other audition, each let go of at
  its own moment rather than the panel holding one moment for whatever it last played. A chord
  is several keys and they are not pressed at one instant, so one moment for all of them meant
  the first key outliving its own hold by however long the hand took.
- A key coming up on a plugin ends that key's note. It ended nothing before: there was no way
  to name one note, so `LetPreview` had a plugin branch that did nothing at all.

The kit is left out on purpose. Its answer to the same question is its choke groups, and a
crash has to ring under the snare that follows it. So BongaBong has no `new_note` on its face
and `TrackMixer`'s pad overload is the one place that still makes no room at all.

Where it shows: `new_note` on Recording, Zampler, OddSkilla and Ouroboros, a three position
switch in each machine's own design, next to the voices switch on Recording and in the
amplifier group on the other three. It is a machine parameter like any other, so it is saved
with a preset, pointable from a controller and automatable, and a machine written by somebody
else can draw it or leave it out.

## Note columns, which were the editor's work

What a Renoise note column carries is the whole cell again: note, instrument, volume, panning,
delay and its own effect (`renoise/song/pattern/line.lua`). Here that means a column is another
`TrackerCell`, unchanged, and nothing about the cell type moved.

How many, and where the count lives. Renoise: `max_note_columns` is 12, `min_note_columns` is 1,
`visible_note_columns` is 1 to 12, and they sit on `renoise.Track`, which is the song, not the
pattern. That was taken as it stands, with eight rather than twelve, because every column is
width on the screen whether or not anything is written in it: a track with twelve is a pattern
where you can see two tracks. `Song.NoteColumns` is the list, one entry per track, and every
pattern is given the song's counts whenever they move.

The pieces, in the order they stopped being invisible.

**The pattern.** One flat array of value types, as before. The stride is the row's total column
count rather than the track count, `_starts` is the running total so a cell's place is an
addition rather than a walk, and `Rebuild` keeps whatever still fits across all three axes.
`MoveTrack` rebuilds the block rather than shuffling it in place, because two tracks need not be
the same width and a move is no longer a swap of equal pieces.

**The file.** A cell entry past the first column is written `"line:track:column:cell"` and the
first column keeps the three-part form it always had. Not for tidiness: a build that predates
note columns splits the entry into three and reads the third field as a cell, so writing the
column number into every entry would leave an older copy of the application finding every cell
unreadable. This way it reads what it can play and leaves behind what it cannot, which is the
bargain the rest of this format makes. Old songs load untouched, no migration and no version
flag, which is what that format was chosen for.

**The sequencer.** `EventsFor` gained an inner walk, `TrackerEvent` names a column beside its
track, and the two memories are per column: the volume must be, or one voice of a chord would
set the level of the others, and the instrument is per column as well, which is Renoise's
arrangement and the only one that holds up once a column is a voice. A song with one column a
track cannot tell the two apart, which is every song written before this.

**The mixer.** `MakeWay` cuts by track and column, `SynthVoice` carries the column beside its
track, and every plugin note is written down per column rather than per track. That last is
what makes an OFF in one column of a chord end one note instead of all of them.

**The cursor and the metrics.** `NoteColumns` is the walk all three places share: where a cell
sits, where it is drawn, and where the next press of Tab lands. Written out three times those
would eventually disagree, and the way that fails is a click landing on a cell other than the
one under the pointer. `PatternMetrics.TrackWidth` is per track now and every horizontal
question is a walk from the left.

**Entry.** Renoise's rule: a note played while another key is still held goes into the same
track's columns on one line, and the cursor steps down once. Where in them is decided by pitch
rather than by the order the fingers landed, which Renoise does not do and which this wants
because a column is a voice that carries across chords: appended in arrival order, the same
shape records as E G B on one take and E B G on the next, and column one is the bass in one
chord and the top of the next.
The track widens itself to fit, which is `Song.RoomForChord` and is not a nicety: a track shows
one column until somebody says otherwise, so without it a chord recorded into a fresh track puts
its second note on top of its first and keeps whichever finger was last down. That reads as
polyphony not working at all, and it is how this was first shipped.
The held-note counting is in the view model, since a hand on the hardware and a hand on the
letter rows are the same hand. The letter rows needed a key-up they never had: a note typed into
the pattern had no release at all, which was enough while a track held one note and is not
enough now. It ends the chord and not the sound, because a note played by hand runs its own
length here.

**History.** `Pattern.Cells`, `Holds` and `Restore` carry the column counts, and a step that did
not would hold cells of the wrong length, be refused, and say nothing. This codebase has had
that exact bug twice and both times it survived because doing nothing looks like working.

## Effort

```
new note action, three actions                    done
per-note plugin offs, and the note bookkeeping    done, and note columns needed it
column axis: pattern, file, sequencer, mixer      done
cursor, metrics, entry, history                   done
per-column selection                              half a day, and not done: see below
```

## Decided already

- New note action first, which is done. It was worth having on its own and it built the
  per-note plugin bookkeeping that note columns cannot do without.
- Three actions, cut, release and sustain, cut being the default. Renoise's set, for Renoise's
  reason: fade needs a rate no patch here has.
- The action belongs to the instrument, beside `OneVoice`, not to the track.
- A note column is a whole `TrackerCell`. No new cell type.
- The column count belongs to the song's track, not to the pattern. One to eight, default one.
- The file grows a fourth field and old songs read as column 0. The first column keeps the
  three-field form, so an older build still reads what it can play.
- A selection covers whole tracks, columns included. Per-column corners are the piece that was
  left; see below.

## Still open

- **A selection is by track and not by column.** `PatternSelection` holds its corners as lines
  and tracks, so selecting inside a track selects all of its columns: copy, cut, transpose and
  the rest carry the whole chord. That is right for most of what anybody does with a block and
  wrong for the one thing Renoise can do that this cannot, which is take hold of one voice of a
  chord and move it. The corners become lines and flat note columns, `Contains` follows,
  `PatternBlock` already carries the columns, and the grid draws and hit tests the block the way
  it already draws and hit tests the cursor. Half a day, and it was left because a selection is
  something people rely on and it is worth doing on its own rather than at the end of a week.
- Whether `MaxVoices`, 48, still holds. Eight tracks of four-note chords in release is over it,
  and stealing the oldest during a sustained chord is audible in a way that stealing during a
  monophonic part is not. Sustain makes this reachable today, before note columns exist at all.
- Whether a track's ending should be readable from the pattern. A track left sustaining looks
  exactly like a track that is not, until you wonder why the mix is filling up.
- What a chord does when it will not fit. A ninth note pushes the rest along and the highest
  falls off the end, since eight is as wide as a track goes. Dropping the ninth itself is the
  other answer; this one at least keeps the chord in order and loses the note furthest from the
  bass.
- Whether the widening should be undoable on its own. It is not: the notes leave a step, so undo
  takes the chord off and leaves the track wide, which is an empty column. A step of its own
  would mean a three note chord costing three presses to undo, two of which appear to do
  nothing.
- Column mutes and column names, which Renoise has. Cheap now that the axis exists, and no use
  at all until somebody has written a chord with them.
- Whether a track's insert chain and a future automation lane stay per track. They should:
  columns share a bus, which is the whole point of them being one track.
