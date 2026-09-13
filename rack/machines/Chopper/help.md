# Chopper

A kit chopped out of one recording. Hand it a drum beat, or a sample pack's sheet of hits played
one after another, and it listens for the drums in it, cuts each one out into a sample of its own,
and puts those on the pads.

It is the same kit engine BongaBong plays, so everything a pad does is the same: nothing is
transposed, pads sound over each other, and choke groups silence each other. What differs is where
the sounds come from. BongaBong is filled with samples you already have; Chopper makes them.

## Chop a recording

**Chop a recording...** asks for a recording off RECORD's shelf. Import the file there first if it
is not on it yet. Then:

1. **It finds every hit.** The bottom, the middle and the top of the sound are followed a few
   milliseconds at a time, and a hit is wherever any of them jumps: a hat played over the tail of a
   kick is still found, because the top of the sound leaps even while the kick is louder.
2. **It hears what each hit is.** Weight under a hundred and fifty cycles that is gone quickly is a
   kick. A short knock in the middle, or a body with a rattle over it, is a snare. Top that stops
   at once is a closed hat, top that rings is an open hat, and top that goes on ringing is a
   cymbal. A tone in the middle that sings is a tom.
3. **It keeps one of each.** A beat plays the same kick again and again; hits that sound alike are
   grouped, and the one with least else ringing on top of it is kept. Ghost notes and the room
   ringing between hits are heard and left out.
4. **It cuts them out.** Each drum is written to a sample of its own, from just before the hit to
   where it has died away or the next hit lands, faded at both ends so it does not click.
5. **It lays them on the pads**, kick first, then snare, hats, toms and cymbals, each named for
   what it is.

The samples are kept in the application folder under recordings, chopped, in a folder named for
the recording. Chopping the same recording again makes a second folder beside the first rather than
writing over it, so a song already using the first chop sounds as it did. A song packed to hand on
carries them.

It is rules, not a model: it listens the way you would, and it is right about drums that sound like
drums. Something built to sound strange can be heard as the wrong drum. **Clear** takes a pad's
sample off, and chopping again starts over.

## What ships with it

**Energy Beat**, a clean drum loop chopped into its kick, snare, two closed hats and an open hat,
with the three hats in one choke group so an open hat is cut short by a closed one.

## Pads

The grid is the kit, one pad to a key from C-4 upwards, so the keyboard at the foot and a pad box
on your desk reach the same sounds. Clicking a pad picks it and the card beside it is about that
one.

- **Level** is that pad's own, nought to one.
- **Pan** is where it sits, hard left to hard right.
- **Choke** is the group, one to eight, or nought for none. Two pads in one group cannot sound
  together, which is what an open and a closed hat want.
