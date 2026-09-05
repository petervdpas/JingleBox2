# Zampler

Recordings laid across the keyboard. Each zone is one recording, the stretch of keys it answers
to, and the key it was recorded at, and every key but that one plays the recording faster or
slower. That is the whole difference between this and a kit: a zone is transposed, a pad is not.

A piano sampled every fourth key is thirteen zones. One take cut into eight pieces is also eight
zones, all pointing at the same file with different windows, which is what the Chop editor makes.

## Map

The strip at the top is the keyboard with every zone drawn on it, and it is where the ranges are
edited: drag an edge to move it, drag the middle to slide a whole zone along, drag the white line
to say which key the recording was made at. Clicking a zone picks it, and the card below is about
whichever one is picked.

- **Add zone** puts another one over the middle octave, empty, and picks it.
- **Remove** takes the picked one off. The last zone cannot go: a map with none is an instrument
  that cannot make a sound.
- **Spread** lays every zone that has a recording out evenly across the playable keyboard, which
  is what you want the moment eight files have been dropped on the machine and you do not care
  where each lands, only that they land somewhere sensible.

Zones are asked in order and the first one covering the key wins, so a narrow zone above a wide
one carves an exception out of it.

## Chop

Turns one recording into pieces, one zone each. **Pieces** is how many. **Cut at** is how the
cuts are found: **Hits** looks for the attacks, **Gaps** looks for the quiet between them, and
**Even** divides the length up regardless, which is right for a bar of a loop and wrong for a
performance. **Chop** does it. **Loop piece** is what a piece does when it reaches its end:
**Off**, **Fwd** or **Ping**.

The cuts are not stored anywhere separately. They are read back off the zones, which is why
putting a different recording on one piece quietly stops it being a chop.

## Zone

Everything on this card is about the one zone picked on the Map.

- The picker at the top chooses it by name.
- **Clear** takes the recording off and leaves the zone. An empty zone makes no sound.
- **Load samples...** asks for files from anywhere and lays them across the keyboard, one zone
  apiece. **Pick a recording** puts one take off RECORD's shelf on this zone alone.
- **Name** is what it is called here and in the picker. Left blank it falls back to the file's
  name, then to the keys it covers.
- **Keys** and **Root** are readouts of the Map: the stretch it answers to, and the key it plays
  untouched at.
- **Level**, **Pan** and **Fine** are the zone's own, before the mixer sees anything. Level is
  nought to one on top of the pattern's volume column, Pan is hard left to hard right, and Fine
  is cents either way, for a take that was recorded slightly sharp or for sitting one zone
  against the next.

## Filter

Four poles, shared by every zone.

- **Cutoff** and **Resonance** are the tone and how hard it rings.
- **Env amount** is how far the filter envelope moves the cutoff. Nought is the envelope
  switched off.
- **Env polarity** decides which way it moves: **Open** is the classic sweep upwards, **Close**
  is a note that dulls as it holds.
- **Key follow** keeps the tone even across the keyboard. At nought the top of the keyboard goes
  dull, since the cutoff stays where it is while the notes rise past it. At one the cutoff rises
  a semitone for every semitone above the zone's root, which keeps a zone even at the cost of it
  changing character between zones.

## Filter envelope

Its own attack, decay, sustain and release, in milliseconds and nought to one. Separate from the
amplifier's on purpose: how long the brightness takes to arrive is not the same question as how
long the loudness does, and the difference between the two is most of what makes a sampled
instrument sound played rather than triggered.

## Amplifier

- **Attack**, **Decay**, **Sustain** and **Release** shape the loudness. Sustain is full by
  default, which is the machine doing nothing: a recording already has its own shape and an
  instrument that quietly decayed every take would be fighting it.
- **Level** is the instrument's own output.
- **New note** says what happens to a note still sounding when the same column plays another:
  **Cut**, **Release** or **Sustain**. A struck sample wants Cut; anything held wants one of the
  other two.

## What ships with it

Countdown, Creepy Radio and Sick Man Sneeze, each a chopped take with its pieces laid out. They
are there to show what a chop is as much as to be played.

Nothing you load is copied into the machine. A zone points at a recording on RECORD's shelf, so
deleting the take there is what takes the sound away, and packing a song is what carries it to
somebody else.
