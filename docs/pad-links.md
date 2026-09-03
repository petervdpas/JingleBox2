# Pointing hardware at the pads

Built on 2026-09-04. This is the design, the piece of new machinery it needed, what it took away,
and what is still open.

## What it is

A pad box is pointed at the pads the way everything else in this application is pointed at
anything: press Ctrl+Shift+M, rest the pointer on a pad on FIRE until it glows, hit the pad on
the hardware. The mapping lands in the same layer every other link lands in, so it turns up on
MIDI CC as a card headed **Pads**, it is exported to a `.jbtl` and handed to somebody, and it is
displaced by the same two rules a link has always kept.

FIRE gets the same menu in its upper right corner the mixer's card already has: the control
surfaces pointed at the pads, and the line that starts learning. Same `ControlMenu`, same
`IMenuLines`, so a line with no command is dead in one place rather than two.

## Why, and it is not that the old page is ugly

The pad mapping page was a **second way of pointing hardware at something**, with its own
storage (`MidiConfig.Pads`), its own Learn button, its own matching rules in `MidiRouter` and
its own idea of what a mapping is. Two ways of doing one thing that answer differently is the
fault this codebase has already paid for once, when `ControlScope.Focused` disagreed with the
default layout about which track a fader drove, and the reasoning there holds here: whichever
is right, they cannot both be.

The second reason is the one that actually costs somebody something today. **A template
travels and a pad mapping does not.** Everything a nanoKONTROL2 does to the mixer can be
written out and sent to another machine; the sixteen numbers that say which pad an MPD218's
pads fire cannot, although they are the most device-specific thing in the whole application and
the thing most worth handing over.

## What was decided

**Replace, not coexist.** The pad mapping page is gone. What is in `MidiConfig.Pads` is read once
on the way in and becomes links, the same way an older song's links are still read.

**A pad link is `ControlScope.Fixed`.** Pad 3 is pad 3 from every page, exactly as strip 3 is
strip 3. It follows nothing.

**One card, headed Pads, with a line per pad.** The mixer is the precedent and the argument is
the same: cut by pad it would be a card per pad saying the same three words with a number
changed, and a file per pad nobody could use.

**A pad is a press, not a value.** It goes with `ControlKind.Action` and `ControlKind.Transport`
rather than with the parameters, so nothing about parking, pickup or turn applies to it. That
rule already exists and is already tested: the two press branches are asked before parking.

## The one piece of new machinery, which is where the work was

**A link can only name a controller number, and a pad box sends notes.** `ControlMapping` has
`Channel` and `Cc` and nothing that says which kind of message it is about, because every link
made before this was a knob, a fader or a button sending CC. `MidiMapping` had the field this
needs, note or controller, and is the type that was retired.

So three things moved:

- `ControlMapping.Sends` says which of the two it is about. Absent means controller, so every
  link already on somebody's disc reads as what it was.
- `ControlMapping.Answers` matches a note on that channel at that number, press only. A note off
  is not a second press, which is the line the old pad router kept.
- `ControlLink.Handle` accepts a note on as the control being learned. It was only ever reached
  from the controller router, so a pad hit while the mode was on reached nothing at all.

Everything else is the mixer's shape again: `PadLinks` beside `MixLinks` and `TransportLinks`,
`ControlKind.Pad`, `ControlTargets.OnPad` reaching the pad's trigger through the `IPadTrigger`
that was already there, `Pointable.Offers` on the pad cell in `UseView`, and `ILinkTargets`
learning the word `pads`.

One thing turned up in the writing that the design had not: **the pads needed a door of their
own on the router.** A port is given the pads and the links as separate jobs in SETTINGS, and a
pad link answering on the knobs' door would have a pad box that was never given the pads firing
them anyway. `MidiControlRouter.Pads` is that door, matching pad links and nothing else, while
`Handle` skips them. One router, one list of links, two gates, which is the settings said back.

And none of the knob machinery is on that path, which is not tidiness either: the press test the
other two press kinds use reads anything under 64 as a button coming up, and for a note that
number is a velocity. A pad played softly would have done nothing.

## What falls out of it

SETTINGS, MIDI was left holding the pad matrix size and the toggle switch, which is not a page.
Both are on Control Surfaces now and that rail item is gone, with `MidiView`,
`PadMidiMappingViewModel`, `LearnButtonTextConverter` and the learning half of `MidiViewModel`.

Worth saying once, since it is the one part of this that is not obviously right: **the matrix
size is not a fact about hardware.** How many pads there are and in what shape is about the
pads, so PADS is arguably its home and Control Surfaces is where it went because that is where
the rest of the emptied page's neighbours are. If it reads wrong there it belongs on PADS
instead, and moving it costs one `Border`.

`ConfigStore` no longer grows and shrinks a list of pad mappings to match the pad count. Links
are sparse: a link naming pad 12 on a matrix of nine simply does not fire, and it is not
forgotten either, so growing the matrix back brings it with it. That is the rule a link already
keeps about a controller that is not plugged in.

## What was left where it was

**The Pads tick on a port stayed.** The old router matched a pad mapping on the kind, the channel
and the number and never on the device, and it was allowed to because the tick had already
established that this port drives pads. A link carries the controller it was learned on, so the
tick is no longer needed for that, but it still says what a port is allowed to do at all, which
is a different question: it is the gate `MidiControlRouter.Pads` is reached through.

**A fresh installation has nothing pointed at the pads.** The table used to be filled in with
notes 36 upwards on channel 1 for every pad whether or not anybody had asked, and
`DefaultLayout` has said the opposite since it was written: a pad nobody has pointed at should do
nothing rather than something surprising. The seeded rows mostly did nothing anyway, since the
pad boxes here send on channel 10. What somebody actually learned is carried over untouched.

## Open

**Whether toggle mode stays one setting for every pad.** It is one today because a pad box is
used one way or the other for a whole show. Nothing here changes that, and per pad it would be
sixteen decisions nobody wants to make.

**Velocity.** A pad hit hard and a pad hit softly are the same trigger here. That is what it
already was and this changed nothing about it.

**The pads on PADS are not pointable, only the ones on FIRE.** PADS is where a pad is filled and
FIRE is where it is played, and the gesture is a hand on the hardware with a pointer on the pad
it should fire. If it wants to be on both, it is one attached property in one more template.
