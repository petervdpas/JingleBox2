# Plugins on Windows

A handover note. Everything here was worked out on the Linux side, where it cannot be tested,
and is written for whoever picks it up on Windows, where it can.

Read `CLAUDE.md` first for how this codebase is written. The house style applies to anything
added here: no em-dashes, no LLM tells, explicit keys rather than interpolated ones.

## The one rule

**Do not touch the Linux side.** It works, and it cannot be tested from Windows, so anything
broken there will be found weeks later by somebody who has forgotten this was ever changed.

These files are Linux's and are not to be edited at all:

- `Audio/Plugins/XEmbed.cs` is the X11 embedding conversation, top to bottom.
- `Audio/Plugins/PluginRunLoop.cs` is the X11 timer and file-descriptor pump that VST3 and CLAP
  ask a Linux host for. Windows needs a message loop instead, which is a new thing beside it,
  not a change to it.

These files are shared, so add to them rather than rearranging them:

- `Views/PluginEditorHost.cs`
- `Audio/Plugins/Bridge/PluginHostProcess.cs`
- `Audio/Plugins/Bridge/PluginProcess.cs`, `BridgedPlugin.cs`, `PluginBridge.cs`
- `Audio/Plugins/PluginHost.cs`

New behaviour goes behind `OperatingSystem.IsWindows()`, next to the Linux branch that is
already there. If something shared genuinely has to change for both, the Linux path must come
out behaving exactly as it did, and say so plainly in the commit message so it can be checked
on the other side.

Do not change what happens on Linux by default, do not tidy the X11 code while you are passing,
and do not "simplify" the bridge protocol. Every odd-looking thing in it was put there by a
plugin misbehaving, and the comments say which one.

## What is wrong

Serum takes the whole application down when it crashes on Windows. It does not on Linux. That
is not a difference between the machines or a fault in the language: it is one line.

`Audio/Plugins/PluginHost.cs`:

```csharp
public static bool Isolated => !OperatingSystem.IsWindows() && !InProcessAsked;
```

On Linux every plugin gets a process of its own and nothing it does can reach the application.
On Windows they are loaded into this process, so a plugin dereferencing a null pointer kills
the app and whatever was unsaved with it. The comment above that line says the reason: the
window embedding was never written for Windows.

So the job is to make `Isolated` true on Windows as well.

## What has already been done, and does not need doing again

- **Scanning is out of process on every platform**, Windows included. `PluginHost.Scan` starts
  a child, which writes a list of plugins to a temp file and exits. Nothing about it needs
  window embedding. This was the worst case: a plugin dying while being asked what it is would
  kill the app on every start, before anybody had chosen to use it.
- **Crash reports.** `Diagnostics/CrashReport.cs` writes a file into `%APPDATA%\JingleBox2\crashes\`
  when the app stops in a way nobody meant, and names the plugin it was in the middle of. On
  Windows it will also say that plugins run in-process, which is the underlying cause. Ask for
  one of those files before guessing.
- **Saves are atomic.** `Config/SafeFile.cs`. A crash during a save no longer leaves a
  half-written settings file or song.
- **Unsaved work is kept.** The tracker writes the song to `<name> (recovered)` in the songs
  folder every twenty seconds while it is unsaved.
- **Both plugin standards already name the Windows window type.** `Vst3Abi.PlatformWindowType`
  gives `HWND` and `ClapAbi.WindowApi` gives `win32`. That part is not a trap.

## What the bridge is made of

One child process per plugin, started as this same executable with `--plugin-host`. Three
channels between parent and child:

| What | Where | Portable? |
| --- | --- | --- |
| Control messages | `PluginProcess.Call`, a Unix domain socket | Yes, `AF_UNIX` works on Windows 10 1803 and later. The path already falls back from `/tmp` to `Path.GetTempPath()`. |
| Audio blocks | `PluginProcess.Render`, a second socket | Same. |
| The audio buffers | `PluginBridge.BridgeBlock`, `MemoryMappedFile.CreateFromFile` | Yes. The folder already falls back from `/dev/shm` to `Path.GetTempPath()`. |
| The plugin's window | `Views/PluginEditorHost.cs` plus `Audio/Plugins/XEmbed.cs` | **No.** This is the X11 half and the only part that has to be written. |

## The plan, in the order I would do it

### Stage one: isolation without the plugin's own window

This needs no Windows-specific code at all, and it is where the crash goes away.

When a bridged plugin has no window, the host draws its own knobs instead. That path already
exists and is used every day: `BridgedPlugin.OpenEditor` returns null when
`PluginProcess.HasOwnWindow` is false, and `PluginControlsViewModel.Prepare` falls back to
`BuildKnobs()`. `HasOwnWindow` comes from one word in the child's greeting, `PluginProcess.cs`
around line 192, sent by `PluginHostProcess` as `window` or `plain`.

So:

1. Add an opt-in, something like `JB_PLUGIN_BRIDGE=1`, and make `Isolated` true on Windows when
   it is set. Leave the default alone until stage two works, so nothing regresses for anybody.
2. Make the child report `plain` on Windows for now, whatever the plugin says.
3. Try it. Serum's own interface will not be there, and a wall of knobs is a poor substitute,
   but the audio, the notes, the parameters and the patch should all work, and a plugin that
   crashes should leave the application standing with an offer to start it again.

What to look for in `%APPDATA%\JingleBox2\jinglebox.log` with `JB_LOG=1`:

```
Opening Serum 2 (VST3), Isolated=True, InstrumentMode=True
starting a process for Serum 2 (VST3) at ...
run loop: N rounds ...; M blocks of audio in the last two seconds, ...
```

If the child never greets the parent, the socket is the suspect. If it greets and then dies on
the first block, the shared memory is. Both write their own lines.

### Stage two: the plugin's window

On Linux the host hands the plugin an X11 window id and then owes it the XEMBED conversation
and a run loop. On Windows it hands over an HWND and owes it neither of those, but owes it a
message loop instead. Roughly:

1. `Views/PluginEditorHost.cs` already asks Avalonia for a native child control and gets a
   platform handle back. On Windows that handle is an HWND. Everything in that file that is
   X11-specific is behind `XEmbed`, so the shape of the class stays.
2. The child calls `IPlugView::attached(hwnd, "HWND")`, which it already does with the right
   platform string. The plugin then creates its own window as a child of that HWND. `SetParent`
   across processes is supported on Windows, which is what makes this possible at all.
3. The child must run a Win32 message loop on the thread that owns the plugin's window.
   `PluginRunLoop` is the X11 equivalent and is not needed here: VST3's `IRunLoop` is a Linux
   interface. What replaces it is `GetMessage` and `DispatchMessage` on the pump thread in
   `PluginHostProcess`.
4. Keyboard input across the process boundary needs `AttachThreadInput` between the parent's UI
   thread and the child's window thread, or the plugin will draw and take mouse clicks but
   ignore every keystroke. This is the Windows counterpart of the bug that cost a day on Linux.

Two things learned the hard way on Linux, which are likely to be true on Windows as well and
are worth checking before writing anything clever:

- **The interface must be built once per plugin, not once per window.** See `OpenEditor` and
  `CloseEditor` in `Audio/Plugins/Bridge/PluginHostProcess.cs`. Tearing it down when the window
  closes took the host's watch on Serum's own connection with it, and Serum never asked for it
  again, so the second window drew perfectly and answered nothing.
- **The plugin must be told when its window becomes the active one, and told again every time.**
  See `Views/PluginEditorHost.cs`. Telling it once at handover is not enough. On Windows the
  equivalent is activation and focus messages, and the same failure looks the same: a window
  that draws and ignores clicks.

## How to know it worked

- Open Serum, play notes, turn knobs in its own window, save the song, reopen it, and the
  sound is what it was.
- Close Serum's window and open it again. It still answers. This is the one that failed on
  Linux and is worth trying twice.
- Make Serum crash. The application should stay up, the panel should offer to start it again,
  and there should be no new file in `crashes\`, because nothing crashed except the plugin.

## What not to do

Do not turn the default on for Windows until a plugin's window has been opened, closed, opened
again and played through. The in-process path is worse but it is known, and there is a switch
either way: `JB_PLUGINS_INPROCESS=1` forces plugins into this process on any platform.
