# Chopper

A kit cut from one recording. A drum beat, or a sample pack's sheet of hits played one after
another, goes in whole and comes out as sixteen pads you can play in any order.

It is the same kit engine BongaBong plays, so everything a pad does is the same: nothing is
transposed, pads sound over each other, and choke groups silence each other. What differs is
where the sounds come from. BongaBong is filled a recording to a pad; Chopper is filled from one.

## Patch

**Patch** is the one recording the kit is cut from, off RECORD's shelf. Import the file there
first if it is not on it yet. Picking it puts the whole recording on the pads' picture underneath,
ready to be cut.

## Find drums

**Find drums** listens to the patch and builds the kit from what it hears: one of each drum, laid
out kick first, then snare, closed hat, open hat, tom and cymbal, and each pad named for what it
sounds like. A beat plays the same kick again and again; this keeps one of them, the one with the
least else ringing on top of it.

It listens the way you would. Weight under a hundred and fifty cycles that is gone quickly is a
kick. A short knock in the middle, or a body with a rattle over it, is a snare. Top that stops at
once is a closed hat, top that rings is an open hat, and top that goes on ringing is a cymbal. A
tone in the middle that sings is a tom. Hits too quiet to matter, the room ringing between beats
and ghost notes, are heard and not given a pad.

It is rules, not a model, so it is right about drums that sound like drums and can be wrong about
anything built to sound strange. Each pad plays only its own hit. Pressing it again starts over,
and **Chop** below cuts the patch the plain way instead, a piece to a pad in the order they play.

## Chop

The picture of the recording is at the foot of the machine, with the cutting under it.

- **Pieces** is how many pads to fill, up to sixteen.
- **Cut at** is where the cuts go. **Hits** finds where each sound starts, which is right for a
  beat and for a sheet of hits. **Gaps** cuts in the silences between sounds. **Even** cuts into
  equal lengths, which is right for a loop that is in time.
- **Chop** does it, laying a piece on each pad from the first, and emptying any pads past the last
  piece. Each piece is named for the drum it starts with, heard rather than read off the file:
  Kick 1, Hat closed 1, Snare 1, and so on.

Every cut is a line on the picture and can be dragged where the finder put it wrong. Clicking the
picture twice adds a cut where there is none and takes one away where there is. The pieces all
play the one recording, so nothing is copied and nothing on the shelf changes.

For one sound to a pad, cut a beat one bar long into sixteen. A longer beat cut into sixteen
gives pieces of a beat each, several drums at once.

## Pads

The grid is the kit, one pad to a key from C-4 upwards, so the keyboard at the foot and a pad box
on your desk reach the same sounds. Clicking a pad picks it and the card beside it is about that
one.

- **Level** is that pad's own, nought to one.
- **Pan** is where it sits, hard left to hard right.
- **Choke** is the group, one to eight, or nought for none. Two pads in one group cannot sound
  together, which is what an open and a closed hat want.
