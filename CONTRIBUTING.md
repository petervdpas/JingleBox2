# Contributing to JingleBox2

Bug reports, ideas, sound devices, presets, controller profiles and code are all welcome.

## The licence your contribution is under

JingleBox2 is licensed under the GNU General Public License, version 2, together with the two additional permissions in `LICENSE.EXCEPTION`: one for plugins and sound devices, and one for linking with the BASS audio library.

By opening a pull request, you agree that what you contribute is offered under exactly those terms: GPL-2.0-only together with both exceptions in `LICENSE.EXCEPTION`, as they stand when your contribution is merged. You also confirm that you wrote it yourself, or otherwise have the right to offer it under those terms.

This matters because of the BASS exception. A permission to link with BASS only covers code whose author granted it, so code merged without it could not be shipped in the same builds as the rest.

Sounds, presets, recordings, songs, controller profiles and device folders are data rather than code. Say in the pull request what licence you are offering them under, if it is not the one above.

## Before you open a pull request

```bash
dotnet build
dotnet test Tests/JingleBox2.Tests.csproj
```

- The build has nought warnings of any kind, and has to stay that way.
- Every public type and member carries XML documentation. A contract is documented on its interface; an implementation says only what is particular to it.
- No `//` comments inside methods. What they would say goes into the documentation above the block.
- A fix comes with a test that fails without it.
- The tests run on Linux and on Windows in CI. Something that works on one and not the other is not finished.

`CLAUDE.md` has the longer account of how the code is arranged and why.

## Reporting a bug

Say which platform you are on, what you did, what you expected and what happened instead. If it is about sound, MIDI or a plugin, switch the log on in SETTINGS, System, repeat the problem, and attach `jinglebox.log` from the application folder (`%APPDATA%\JingleBox2` on Windows, `~/.config/JingleBox2` on Linux).
