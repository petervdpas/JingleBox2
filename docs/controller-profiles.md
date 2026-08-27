# Controller profiles

Not built. The answer to a real problem found while wiring a MiniLab 3, written down with the
evidence that produced it.

## The problem

A link is a promise about a number: channel 1, controller 74. The number belongs to the device,
and the device changes it.

A MiniLab 3 has a DAW mode. Switching into it rearranges what every control sends. Measured on
one, with `aseqdump -p 20:0`:

```
its ordinary mode     encoders on CC 16, 18, 19, 71, 74, 76, 77, 93
its DAW mode          encoders on CC 86, 87, 89, 90, 110, 111, 116, 117
```

Not one number in common. Eleven links made in one mode answer to nothing in the other, and
there is no way for the application to notice: same port, same name, different numbers. Nothing
is broken; it is what a DAW mode is. But it means a layout is only as durable as a setting on
the hardware that the software cannot see.

The device also splits itself across two ports. Its transport buttons speak Mackie Control on a
second port while everything else stays ordinary MIDI:

```
Minilab3 MIDI       ch10 notes 36-43 pads, encoders, sliders, pitch bend, CC 1
Minilab3 MCU/HUI    note 86 cycle, 93 stop, 94 play, 95 record   (0x56, 0x5D, 0x5E, 0x5F)
```

So one controller is two devices as far as the settings are concerned, and a person has to know
that to tick the right boxes.

## One device, measured

A MiniLab 3 in its DAW mode, every control worked once in physical order and read off the wire.
This is what a profile for it would say, and it is here so that writing one does not mean
plugging the device back in.

```
encoders   two rows of four, and the numbers interleave by column
             top     CC 86, 87, 89, 90
             bottom  CC 110, 111, 116, 117
sliders    CC 14, 15, 30, 31
pads       channel 10, notes 36 to 43
keys       channel 1
strips     pitch bend, and CC 1
transport  on the other port, Mackie Control: 0x56 cycle, 0x5D stop, 0x5E play, 0x5F record
           and behind Shift: those buttons are the pad bank and screen controls unless it
           is held, so a bare press sends nothing at all on either port
```

That last line cost half an hour. The port was open, the role was ticked, the notes had been
seen on the wire an hour earlier, and pressing play did nothing, because on this device play is
Shift and play. A profile should say which controls need a modifier, not to send one, but so
that a person setting the thing up is told rather than left to conclude the software is broken.

And the thing worth knowing before anybody writes the file: in DAW mode those encoders send
**absolute** values, walking smoothly. In the device's ordinary mode the same knobs send the
same number over and over, which is a relative encoder counting notches. So a mode changes not
only what a control is called but what kind of control it is, and a profile has to say both.

## The shape

A file that describes a controller, the way `machine.json` describes a machine. The app already
believes in this: a machine says what it is and the application draws and plays it without
knowing anything in particular about it. A controller can say what it is on the same terms.

```
controllers/minilab3.json

  name        Minilab 3
  ports       controls  "Minilab3 MIDI"
              transport "Minilab3 MCU/HUI", protocol mcu
  pads        channel 10, from note 36, eight of them
  strips      pitch bend; modulation on CC 1
  sliders     CC 14, 15, 31
  encoders    eight, and what each sends per mode
                ordinary  16, 18, 19, 71, 74, 76, 77, 93
                daw       86, 87, 89, 90, 110, 111, 116, 117
```

## What it changes

**A link names a control, not a number.** `minilab3/encoder3` rather than `CC 89 ch 1`. Change
the device's mode and the layout still answers, because the profile knows both dialects.

**The mode detects itself.** If CC 86 arrives and the profile says 86 is encoder 1 in DAW mode,
the application knows which mode the device is in. Nobody is asked, and the device is not
consulted. This is the same trick `ControlSense` plays on a stream of values, applied to
identity instead of behaviour.

**The list reads as the hardware reads.** `Encoder 3 · Ouroboros cutoff` rather than
`CC 89 ch 1 · Ouroboros cutoff`. What is printed on the front of the device is what the screen
says.

**One controller is one row.** Its two ports are one device in SETTINGS, ticked once, rather
than a person having to know that the transport lives somewhere else.

**`ControlSense` gets a shortcut, not a replacement.** A profile that says a control is an
endless encoder saves the three messages of listening. It stays for everything with no profile,
which will always be most things.

## Storage, and what happens to existing links

A `ControlMapping` names `Device`, `Channel` and `Cc` today. It would gain a control name, and
keep the number as what it was learned as. A mapping with a name is resolved through the
profile; one without is resolved as it is now. Nothing already saved stops working, and nothing
has to be converted.

Profiles ship beside the program and are copied into the application folder on first run, the
way machines are, so somebody can write one for their own controller without rebuilding
anything. `MachineRegistry` is the pattern to copy, including the part where the shipped copy
is a source rather than the answer.

## Effort

```
the file, reading it, and the registry that holds them     a day
resolving a link through a profile, both ways              half a day
mode detection from an arriving number                     hours
the settings page showing one device rather than two       half a day
one profile for the MiniLab 3, written from the dumps      hours
```

Roughly the machine description system in miniature, which is the right size for what it buys.

## What is built already

The transport half of this exists, without profiles, because it needed nothing from them.
`MidiTransportRouter` reads Mackie Control's transport notes off whichever port is ticked for
`MidiDeviceRole.Transport` in SETTINGS, and works the same `TransportSwitch` the caps and the
space bar use, so it is page sensitive without knowing it. Cycle, rewind and forward are
recognised and named in the log while doing nothing, so finding a use for one is a line in a
switch rather than a rediscovery of the protocol.

What profiles would add on top is the naming: `Encoder 3` instead of `CC 89`, one row in
SETTINGS instead of two, and a layout that survives the device changing mode.

## Still open

- Whether the transport port needs MCU implemented properly or whether reading four notes is
  enough. Four notes is enough to start, and the rest of MCU is bidirectional: the host is
  expected to talk back, which is how the device's screen and encoder rings would light up.
  That is a bigger and much more interesting piece, and it belongs in its own document if it
  is ever wanted.
- Whether a profile should describe pads as well, so the pad mapping page could name them
  rather than asking somebody to press Learn eight times.
- What to do when a profile and the hardware disagree, which will happen the first time
  Arturia changes a firmware. The measured number should win over the file, and the file should
  be the thing that gets corrected.
