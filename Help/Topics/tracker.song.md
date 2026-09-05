# Songs

What a song holds, where it is kept, and how one travels.

A song is a `.jibx`, which is a zip: the patterns, the order, the mix and a copy of
every instrument it uses, plus each plugin's own patch as that plugin handed it over.

The patches are kept beside the document rather than in it because they are almost
all of it, and because a document is all or nothing: one song here is 348 KB of which
the music is 781 bytes and one synth's patch is 331 KB, and a patch that came back
damaged used to cost the whole song.

## Saving

**Save song** writes it where it lives. **Save as...** writes it somewhere else and
works on that one from then on. **Cancel changes** reads the song back off disc as it
was last saved, and asks first.

Both buttons colour when there is something on screen that is not on disc: green on
the safe one and warm on the other, since the moment saving starts asking to be
pressed is the moment discarding starts being able to cost you an afternoon.

## Recordings a song uses

A recording that lives in the application folder is written down by name rather than
by path, so a song survives that folder moving or being opened on another machine.

**Pack...** writes the same `.jibx` with the recordings inside it, wherever you
choose and never into the songs folder. Saving does not do this, because a song built
on a long take is tens of megabytes and the open song is written out every twenty
seconds.

What travels is decided per recording: a machine's own presets ship with the program
and are named rather than carried, and your own takes are carried. Opening a packed
song puts what it carried on the shelf and repoints the instruments, skipping
anything already there byte for byte, so opening one twice adds nothing.

**Import...**, on the Open song dialog, is how one arrives. It takes a `.jibx` from
anywhere on the machine and copies it into your songs, under a free name if that one
is taken, so nothing you already have is overwritten. There is no unpack of its own:
whatever the file carries arrives when you open it, which is the same press every
other song in the list takes.

**What a pack does not carry is somebody else's plugin.** A VST3 or a CLAP is another
program and has to be installed on the machine you are carrying the song to. What the
song does hold is that plugin's own patch and where its knobs stood, and a plugin is
looked for by its own identity before its path, so a bundle living somewhere else on
the other machine is still found. A plugin that is not installed there is named rather
than passed over, and the rest of the song still plays.

## Machines a song needs

A song carries its instruments but not the machines they are on. One that is not
registered here makes no sound and has no panel, the status line names it as the song
opens, and opening that instrument says so rather than showing an empty frame.
Adding the machine is SETTINGS, System.
