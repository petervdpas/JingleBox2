# Audio plugins

CLAP and VST3 plugins this machine has.

Plugins installed on this machine, in either of the two formats this app hosts.

CLAP is the newer one, with a plain C interface. LSP, Surge and Vital ship it, and
on Linux it lives in ~/.clap and /usr/lib/clap. VST3 is the one nearly everything
ships, Serum included, and it lives in ~/.vst3 and /usr/lib/vst3.

Effects from either format go in the same chain, side by side, on a pad or a tracker
track. Instruments are a different thing: they take notes rather than audio, so they
are kept out of effect chains and turned into tracker instruments instead, on the
INSTRUMENTS page. Only VST3 instruments can be played so far.

Windows plugins are not Linux plugins. A Windows VST3 holds a .dll and needs wine
and yabridge to run at all; what is listed here is what runs natively.

A plugin draws its own interface where it has one, in a window of its own that you
can leave open while you work. The host's knobs are the fallback for a plugin that
draws nothing, and are what a plugin gets on a platform where its window will not
open. Plugin windows are X11 only so far.

Every plugin runs in a process of its own, and so does the scan. A plugin that
falls over takes nothing with it: the effect passes its audio through untouched or
the instrument goes quiet, a note says which plugin stopped, and there is a button
to start it again with the settings it had. Nothing else in the app notices.

Scanning opens each plugin to ask what is inside it. They stay loaded until the app
closes, on purpose: unloading plugin libraries after they have been used is what
makes hosts crash.

Folders of your own are searched before the standard ones, and are kept with the
rest of the settings.
