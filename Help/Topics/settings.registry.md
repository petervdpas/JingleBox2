# The registry

What this installation has. A device is added here or removed here, and nothing else decides.

A **device** is a soundmachine or an effect. You make one in the designer, or somebody hands you
one as a zip. It is a folder with a manifest at the top of it, and it is not part of the program:
what is part of the program is the **engine** it plays, and the manifest says which one it wants.

## The whole flow

**Designer, registry, rack, song**, in that order, and each step only sees the one before it.

1. A device is **made in the designer**, or imported from a zip.
2. It is **registered** here. That is the only list that answers whether this installation has it.
3. A registered device can be **on the rack**. Unregister it here and it comes off the rack.
4. **A song can only use what is on the rack.** A soundmachine taken into a song becomes an
   instrument; an effect becomes a slot on a track's chain.

Nothing skips a step. A device that is not registered is not on the rack, and a device that is
not on the rack cannot be given to a song.

## Adding and removing

**Add** registers a device this installation ships with. **Remove** unregisters one, and that is
not losing it: the shipped copy stays where it was and can be taken again. **Import a machine...**
and **Import an effect...** read a zip somebody handed you.

Two folders and only one of them is yours. Beside the program is what ships, which is a source to
take from and is never written to. Under the application folder is what this installation
actually has.

What is recorded is what has been *offered*, not what is present. So a device written after your
folder was made still arrives on a new version, and one you threw out stays thrown out.

A device that ships is kept up to date file by file against the shipped copy, and **nothing is
ever deleted**. What ships is overwritten, because that is the device; anything else in the
folder is yours, which is how a preset you saved onto a soundmachine survives the next version
of it arriving.

Because that comparison is made file by file on each file's clock, a copy you have edited here is
never overwritten by an older shipped one. If you want the shipped version back, remove the device
and add it again.

**Making devices of your own is what the designer is for, and the one rule is the id.** A device
is known by its id and by nothing else, so one of yours carrying the id of a device that ships is
that device here, and the next start brings the shipped copy over the top of it. Give yours its
own id and nothing in this pass will ever touch it: it only walks the files that ship, so your
device and everything in its folder is not looked at.

## Soundmachines and effects are two lists

A soundmachine is played: notes go in and sound comes out, and in a song it becomes an
**instrument**, which is your name, your settings and its own id. An effect is not played: a
whole track's audio goes in and comes back changed, and in a song it is a slot on a track's
chain, with no name of its own.

Registered and on the rack are the same thing for an effect, since there is nothing of yours in
one. A soundmachine can be taken off the rack and put back, because what sits on the rack carries
your settings.

## When one will not appear

A device names its engine in its manifest, and the engines are in the application. So a device
asking for an engine this build has not got is read and passed over rather than refused, which is
what makes a folder from a later version harmless: it stays on the disc and is simply not on the
rack.

That is the only reason a device is passed over. What it is called and what its id is are yours,
so a soundmachine you design under a new id reaches the registry, the rack and your songs like
any other, as long as it names an engine that is here.

The five that shipped before a device could name its own engine say nothing in that field, and
are still understood, so every song and setting you already have keeps working.
