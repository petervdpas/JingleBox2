# Patchbay

Where sound comes from, where it goes, and how to patch it.

The patchbay is the mixer's second tab, and it is this application's own signal path
drawn from its own point of view. Every block on it either feeds JingleBox2 or is
JingleBox2, and every cable has us at one end, so there is nothing on this page that
can unplug your speakers.

## The blocks

Down the left is everything on this machine with audio to give: a capture device, an
output's monitor, or one program that is playing right now. A program only appears
while it is making a sound, so a browser that is paused is not in the picture.

In the middle are the parts of this application. **RECORD** takes the capture in and
gives out the take you are auditioning. **TRACKER** gives out one pair for every track
in the song, named the way the strips on the desk are named. **FIRE** gives out the
pads. **MIXER** takes all of those in and gives out the master, and that runs to the
output device the engine is playing through.

A block drawn in the accent colour is one of ours. Its title bar is the same colour,
so a glance says which half of the picture you are looking at.

## The cables

A dot is one channel, so a stereo point is two dots with two names under each other,
`_FL` over `_FR`, spelled the way the sound server spells them.

Drag from a dot to make a cable. A dot that already has one hands you that cable
instead of starting a second, so the far end stays where it is and the end you took
follows the pointer: that is how a cable is moved from one point to another. Let go
over nothing and it comes out.

**A cable that is carrying audio is drawn solid, and a quiet one is dashed.** So the
page says what the application is doing rather than only how it is wired: the track
that is sounding is solid and the empty one beside it is not.

The cables between our own blocks wear a different colour from the ones you patched,
and they cannot be pulled apart. That is not a limitation to work around, it is what
this program is: the pads reach the desk because a desk is what they are summed on.

## The sidebar

Touch a block to see what it is, what it takes in, and what it gives out. Its meter
is there too, and for the tracker that is one meter for every track at once, since
what a sidebar is asked is whether audio is coming out of the block. Which track is
which is answered on the picture, by the cables that are solid.

Every output has a mute and a solo beside it, and they are the same switches the desk
has: pressing M here is pressing M on that strip. Where a switch means nothing it is
dark rather than missing, so the rows stay the same shape whatever block you are
looking at. A block on the machine has neither, and no meter: somebody else's program
is not something this application measures or can silence.

## Moving about

Drag a block by its middle to move it, and where you leave it is remembered between
sessions. A block you have never moved opens where it was meant to.

Hold Ctrl and Shift, or use the middle button, and drag to move the whole page under
the blocks. That is the same press every picture in this application is panned with.
Panning changes nothing you have arranged; it only changes where the window is
looking.

The handle between the picture and the sidebar gives either of them more room.
