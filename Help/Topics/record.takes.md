# Recordings

Making a take, trimming it, and where it goes.

RECORD captures from whatever the input picker is set to. Everything else in the
application takes its sounds from the shelf below: a pad, a machine's preset, a
tracker instrument.

`Ctrl+R` starts a recording and stops it again, from any window.

## The scratchpad

**A take is not on the shelf until you name it.** What you record lands on the
scratchpad, which is the card of its own between the controls and the list: play it,
and either **Save** it under a name or **Throw it away**. Until you save it, it is in
no list, under no search and filed under nothing.

The scratchpad holds one take. Recording again starts again, so what was on it goes,
and so does anything still on it when you close the application. That is the whole
meaning of the word: a take you did not name is a take you did not want.

A name already on the shelf is refused rather than numbered, so saving can never
write over a recording you already have.

## Recording through effects

**Recording Effects** is a chain like a track's or a pad's: press the plus and put our own effects or a plugin on it, and every take goes through it on its way to the shelf.
It stays where you leave it, the way the input gain does, so the microphone through a compressor and a delay is how the room is wired until you change it.

**Both go on the scratchpad**, and the two buttons above the picture pick which one
you are looking at and would play. Saving keeps both: the name you typed is the take
through the chain, and the capture exactly as it arrived goes beside it as
`<name> (clean)`, because an effect cannot be taken off a take afterwards. With
nothing on the chain there is one take, since the two files would otherwise be the
same audio under two names.

The two are the same length to the frame, so they lie on top of each other. That is
also why a delay still ringing at the last frame is cut off with it rather than
running on.

**The take is made from what arrived and nothing else**, and the chain is run over it
once it has been stopped. That is deliberate: a plugin taking a moment longer than
usual on the way past would be a hole in the only copy of a performance, and offline
it can take as long as it likes and makes exactly the same sound.

You can still hear it while you play. **Hear it**, at the foot of the IN strip on the
mixer, puts what is coming in through this same chain and out of the master, so a
microphone through a pitch effect is heard as the pitched thing. That is a second run
of the chain on its own path and it cannot reach the take: what is written is still
the capture as it arrived, worked on afterwards.

Two things to know before you use it. What you hear is a capture buffer and an output
buffer late, which is what monitoring through a computer costs and is why the sizes in
SETTINGS matter. And what an output is playing cannot be heard this way at all, since
that source is the output's own monitor and hearing it through the output would feed it
back into itself. The switch stays live: it says whether the recorder is heard, and what
the recorder is carrying is a separate question. The capture is left off the recorder's
bus instead, and the status line says so.

## Editing a take

Edit opens the take on its own picture, with a box of tools down the left. Picking a
tool shows what it does and the button that does it, in the panel under the box: that
panel is the only place an edit happens from, so nothing is one stray click.

**A copy of the take is taken when the window opens and every tool works on the copy.**
The take on the shelf is not touched at all until **Save**, which is the green button at
the foot. Close asks first if there is anything unsaved, and answering no leaves the
window exactly as it was.

Drag across the picture to mark the part you want, either way round, and the two lines
that appear are the ends of it: take hold of either to move it. What falls outside is
dimmed, so what you are looking at is what would survive. **Select whole take** puts
both ends back on the two ends of the recording.

The tools, in the order they stand in the box:

- **Trim** keeps what is marked and throws the rest away.
- **Silence** empties what is marked and leaves the take its length, for a cough in the
  middle of a good one. Nothing that points into this file moves.
- **Reverse** turns what is marked back to front. Mark the whole take for a reversed
  cymbal, or one word for a word said backwards.
- **Fade in** comes up from silence at the head of the marked part and is at full by its
  end. **Fade out** is the same the other way about. Both are straight.
- **Normalize** lifts the **whole take** so its loudest moment sits on the peak beside
  the button. A take already there is left alone and says so.

## Going back

Everything you do is a line in the **History** at the foot of the rail, with the take as
it was found at the top of it. The two arrows beside the heading go back a step and
forward again, and `Ctrl+Z` and `Ctrl+Y` do the same; picking a line goes there outright.
Lines in front of where you are standing are drawn faintly and are still offered, since
that is what going forward would do again. Doing something new from where you are
standing drops them, which is what every editor does: they were done to audio that is
no longer there.

**Revert** throws the lot away and puts the copy back to what is on the shelf.

Going back is the take copied again with the steps before that point done again, so
what it costs is a moment on a long take rather than nothing. What it buys is that no
edit here needs an undo of its own, which is where this sort of thing usually goes
wrong.

## Looking at it

The magnifying glasses take you closer and further out, about the middle of what is on
screen; the wheel does the same, about the pointer, and **Fit** puts the whole
recording back on the screen without touching what is marked. To move a zoomed picture
sideways rather than mark a new region, drag with `Ctrl` or `Shift` held, or drag with
the middle button.

It goes in a long way: at the far end a sixteen second take shows about forty
milliseconds across the window, which is close enough to find a click and mark it out.

**Play plays what is marked**, from its beginning to its end, and the cursor runs
across it. Moving an end while it is playing moves what is playing with it, so
dragging the end back to where you are listening stops it: what is playing is the
region, so the region is what decides. What you hear is the copy with your unsaved work
on it.

Under the picture, the selection is written out as times, and beside them what the file
is: its rate, how many channels it has and how many samples.

It is the same picture everywhere. The one on the RECORD page, the one here, and the
one on a machine's face are one control, so a take looks the same and behaves the
same wherever you meet it.

## Deleting

Deleting a take does not delete it. It moves into a bin beside the recordings, and
undo on this page fetches the last one back, which is why the question it asks no
longer has to say that this cannot be undone.

Only this session's deletions are offered back. Putting back a take from last week is
a filing cabinet rather than undo, and what is in the bin from before is emptied
deliberately.

## What is using one

Before a take is deleted the application asks both the rack and the songs whether
anything plays it, and says so. A song owns its instruments, so a recording nothing
on the rack plays can still be the sound of three songs.
