# Log

What the app writes down about itself when asked.

Off, nothing is written and nothing is slowed down. On, the app writes what it is
doing to jinglebox.log, next to the settings, and so does every process a plugin
runs in. Each line carries the time, what it is about, and which process it came
from, so a plugin falling over is next to what the app was doing at the time.

The way to use it is to turn it on, do the thing that goes wrong, turn it off, and
read the file. Leaving it on is not harmful: the file starts again from empty when
it reaches a few megabytes, and the one before it is kept alongside.
