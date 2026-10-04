#!/bin/sh
# Starts JingleBox2 inside the Flatpak sandbox.
#
# The application keeps everything in $XDG_CONFIG_HOME/JingleBox2, which inside the sandbox is
# ~/.var/app/io.github.petervdpas.JingleBox2/config. Every other way of installing it uses
# ~/.config/JingleBox2, and the manifest grants that folder, so the sandbox's copy is a link to
# it: one folder of recordings, songs and devices, whichever package started the program.
set -eu

shared="$HOME/.config/JingleBox2"
own="${XDG_CONFIG_HOME:-$HOME/.var/app/io.github.petervdpas.JingleBox2/config}/JingleBox2"

mkdir -p "$shared"
if [ ! -e "$own" ] && [ ! -L "$own" ]; then
  mkdir -p "$(dirname "$own")"
  ln -s "$shared" "$own"
fi

APPDIR=/app/lib/jinglebox2
export LD_LIBRARY_PATH="$APPDIR${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"

exec "$APPDIR/JingleBox2" "$@"
