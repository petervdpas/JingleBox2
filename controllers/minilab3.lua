-- MiniLab 3
--
-- A controller codec. One of these sits between a device and the rest of JingleBox2, and its
-- whole job is to say what a message means. It cannot add a feature and it cannot take one
-- away: it can only translate. A device with no codec works exactly as it always did, which is
-- why writing one of these is optional for ever.
--
-- Save this file and it is reloaded at once. No restart, no replugging.
--
-- The Lua here is 5.2. The one thing that catches people out: there are no >> and << operators,
-- because those arrived in 5.3. Use bit32.rshift, bit32.lshift, bit32.band and bit32.bor, which
-- is what a codec needs anyway. A script that will not parse is reported in the log and then
-- left alone, so its device carries on working as though the file were not there.

controller = {
  -- What SETTINGS should call it.
  name = "MiniLab 3",

  -- Which ports it is about. A star stands for anything, because a port is called
  -- "Minilab3 MIDI" on Linux and something with a number in front of it on Windows.
  matches = "Minilab3*",
}

-- Called once per message, before anything else in the program sees it.
--
--   m.device   which port it came from
--   m.type     "note", "cc" or "bend"
--   m.channel  1 to 16
--   m.number   the note or controller number. 0 for a bend, which has none
--   m.value    velocity, or a controller value, 0 to 127.
--              for a bend, 0 to 16383 with 8192 in the middle
--   m.on       a note on, or a controller above nought
--
-- Return nothing and the message stands as it arrived. Return false and it is swallowed.
-- Return a table and that is read instead.
--
-- You can also call:
--   log("something")     into the application log, under MIDI
--   send(0xF0, ..., 0xF7)  or  send({0xF0, ..., 0xF7})   bytes back to the device

function midi(m)

  -- The pitch strip. It sends pitch bend, which nothing in this application listens to, so
  -- until now the left hand strip did nothing at all. Turned into an ordinary controller it
  -- becomes something you can point at a knob like any other control.
  --
  -- 14 bits down to 7, and the bottom half of the strip reads as the bottom half of the range,
  -- which is not what a pitch wheel wants but is exactly what a fader wants.
  if m.type == "bend" then
    return { type = "cc", channel = m.channel, number = 2, value = bit32.rshift(m.value, 7) }
  end

  -- Everything else is already something the application understands. Saying nothing is how a
  -- codec stays out of the way, and it is the path almost every message takes.
end
