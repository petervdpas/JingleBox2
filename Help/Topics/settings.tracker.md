# Tracker

Which parts of the tracker are switched on.

Each part here can be switched off on a machine with little to spare, such as a
Raspberry Pi. Off means the part is not shown **and** its work is not done, both: a part
that disappeared from the screen and went on working behind it would be telling you one
thing and doing another. A switch takes effect at once and is kept for the next start.
Nothing in a song is changed by it, so a song saved with a part off opens with that part
whole on a machine that has it on.

- **Pattern automation** is the automation strip under the pattern. Off, the strip is
  gone, and the lanes a pattern holds are kept but neither played nor recorded: an
  instrument or an effect stays wherever it was last set.
- **Command editor** is Command... on the pattern's right click menu. Off, the line is
  gone from the menu. A command can still be typed into a cell as a letter and two digits.
- **Neighbouring patterns** are the faded patterns before and after the one you are on,
  in song mode. Off, the room above and below is left empty and they are not drawn,
  which saves most of the drawing while a song plays.
- **Chain readings** are the first controls and where they stand, printed on each block
  of the instrument/effect chain and of the mixer's chains. Off, they are not read. Reading
  one off a plugin is a question to that plugin's own process, so a chain of plugins is
  where this saves the most.
