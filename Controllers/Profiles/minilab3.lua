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

  -- Nothing, for now, and the reason is worth leaving here because it is what a codec is for.
  --
  -- This file turned the pitch strip's bend into controller 2, since pitch bend reached nothing
  -- in the application and the strip did nothing at all. It is the pitch wheel now: it bends the
  -- notes the keys beside it are playing, with no profile, no link and nothing stored. Converted,
  -- it would arrive as a controller nobody is pointed at and do nothing again, which is the one
  -- thing a codec must not quietly cause.
  --
  -- Put the two lines below back if you would rather point that strip at a knob than bend with
  -- it. Save the file and it is reloaded at once, so it costs nothing to try both.
  --
  --   if m.type == "bend" then
  --     return { type = "cc", channel = m.channel, number = 2, value = bit32.rshift(m.value, 7) }
  --   end
  --
  -- Saying nothing is how a codec stays out of the way, and it is the path every message takes
  -- here. A device with no codec at all behaves exactly as this one now does.
end
