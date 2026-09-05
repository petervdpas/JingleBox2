# Audio plugins

CLAP and VST3 plugins this machine has.

Plugins installed on this machine, in either of the two formats this app hosts.

CLAP is the newer one, with a plain C interface. LSP, Surge and Vital ship it, and
on Linux it lives in ~/.clap and /usr/lib/clap. VST3 is the one nearly everything
ships, Serum included, and it lives in ~/.vst3 and /usr/lib/vst3.

Effects from either format go in the same chain, side by side, on a pad or a tracker
track, and beside the effects this application ships. Instruments are a different
thing: they take notes rather than audio, so they are kept out of effect chains and
are picked as a track's instrument instead, from the rack beside the pattern. Only
VST3 instruments can be played so far.

Windows plugins are not Linux plugins. A Windows VST3 holds a .dll and needs wine
and yabridge to run at all; what is listed here is what runs natively.

A plugin draws its own interface where it has one, in a window of its own that you
can leave open while you work. That works on Windows and on Linux under X11: the
plugin's window belongs to the process the plugin runs in and is put inside one of
ours, which on Windows also means sharing the keyboard with it, so a preset name
typed into a plugin arrives in the plugin. The host's knobs are the fallback for a
plugin that draws nothing, and for anywhere a window will not open, such as Wayland
without XWayland.

Every plugin runs in a process of its own, and so does the scan. A plugin that
falls over takes nothing with it: the effect passes its audio through untouched or
the instrument goes quiet, a note says which plugin stopped, and there is a button
to start it again with the settings it had. Nothing else in the app notices.

Scanning opens each plugin to ask what is inside it. They stay loaded until the app
closes, on purpose: unloading plugin libraries after they have been used is what
makes hosts crash.

Folders of your own are searched before the standard ones, and are kept with the
rest of the settings.

A plugin cannot be pointed at with a controller, and that is a decision rather than a
gap. A plugin is somebody else's program and brings its own MIDI learn, which it
keeps itself, so a link made here would be a second mapping beside the plugin's own
with no way to make the two agree. Remote control is for the machines and effects
this installation owns, and for the mixer. `Ctrl+Shift+M` on a plugin's window says
so rather than doing nothing.

Behind **Knobs** in a plugin's window is a control per parameter, drawn by this
application. It is the fallback for a plugin that draws nothing, and it is also how
you see what a plugin is holding: the knobs are built the first time you ask and
never otherwise, since reading two thousand parameters into two thousand controls is
a visible pause and Serum answers with 2622.
