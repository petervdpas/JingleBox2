# A plugin showing the host's knobs instead of its own face, on Windows

A note for whoever picks this up on the Windows machine. Written on Linux, where the
symptom does not appear, so everything below is either read off the code or ruled out
by reading it. Nothing here has been observed on Windows.

**State it was written against:** `ff5cb4e`, working tree clean.

## The symptom

Serum opens with this application's own panel of knobs rather than its own interface.
Reported as "the serum vst start showing 'our' knobs instead of the serum's ui".

Both are legitimate states, which is what makes this quiet: the knobs are the
deliberate fallback for a plugin that draws nothing, and nothing on the screen says
which of the two you are looking at or why.

## What that means, exactly

`PluginControlsViewModel.Prepare` sets `Editor` and everything follows from it:
`ShowsFace` is `Editor != null && !ShowsKnobs`, and `CanShowKnobs` is `Editor != null`.
So the knobs showing with **no Knobs button in the window's header** means `Editor` is
null: the host was never given a face to switch away from. The knobs showing **with**
the button means somebody pressed it, which is not a fault at all.

Check that first. It costs a glance and it halves the problem.

## Ruled out from here

**The crash guard is not it.** `PluginCrashGuard.IsBlocked` returns false whenever
`IPluginHost.Isolated` is true, and Windows has been isolated since the bridge was
made to run on every platform. A plugin on the blocked list is no longer refused a
window there, and the list in SETTINGS, Plugins has no effect on this. This was the
first theory and it was wrong; it is written down so it is not tried twice.

**The platform window type is right.** `Vst3Abi.PlatformWindowType` answers `HWND` on
Windows, `NSView` on macOS and `X11EmbedWindowID` otherwise. A plugin refusing the
window type would be a real cause and it is not misconfigured.

## The chain that ends in the knobs

Three places can end it, each in one line of code:

1. `BridgedPlugin.OpenEditor` returns null unless `process.HasOwnWindow`.
   That flag is set in `PluginProcess.Greet` from the child's first message, which
   `PluginHostProcess` sends as the word `window` when what it loaded is an
   `IPluginWindowSource` and `plain` otherwise. `Vst3Plugin` implements it
   unconditionally, so `plain` for a VST3 means the child did not load Serum as one.

2. The parent asks the child to open the editor and waits
   `PluginBridge.WindowTimeoutMilliseconds`, which is **8000**. An answer that is not
   `Ok` gives null, and so does no answer at all.

3. In the child, `Vst3Plugin.OpenEditor` is `Vst3Editor.Open`, which gives null for
   three reasons and says which:

   - `editor: no view: the plugin's controller does not offer one`
   - `editor: no view: the plugin was asked for its editor and gave nothing back`
   - `editor: no view: the plugin will not draw into a HWND`

## What to do

Switch the log on in SETTINGS, System, open Serum, and search the log for `editor:`.
The child writes to the same file as the application, so its lines are in there.

- **One of the three `no view` lines.** That is the answer: Serum's own controller
  declined, and the branch names which way. Nothing further needs guessing.
- **No `editor:` line at all.** It never got as far as asking. That is suspect 1 or 2
  below.
- **`editor: the plugin has a view, and it wants N by M` and then nothing.** The view
  was made and the handover is where it stopped. `Vst3Editor` writes a line at every
  step of that, deliberately, so a **missing** line is evidence: see
  `editor: about to hand the plugin window ...` and the answer line after it. A path
  where every branch writes a line is one where silence means the call never returned,
  which is how the original Windows fault was found.

## Suspects, in the order worth trying

1. **The eight second patience.** Serum is heavy, the child process is cold, and it
   has no toolkit to have warmed anything. Eight seconds to build that view is not
   obviously generous. **Do not just raise the number**: time it first, because the
   number is short on purpose. The remark on it says why, that the plugin's window and
   its answers share a thread over there, so a locked up interface stops answering and
   a long wait is a frozen application rather than a message. If it really is the
   timeout, the honest fix is to answer the open asynchronously rather than to wait
   longer.

2. **The child failing to load Serum as a VST3 at all**, which shows as `plain` at
   hello and no editor lines anywhere. That is a different fault wearing this one's
   clothes and the parameters would be empty too, which is visible: the knobs panel
   would have nothing in it.

3. **Something in the handover.** Less likely, because that path is loud.

## What not to do

Do not turn isolation off to make it work. `JB_PLUGINS_INPROCESS=1` exists for
diagnosis and a plugin in this process is one that can take the application down; it
would also put the crash guard back in play and change the symptom for reasons that
have nothing to do with the cause.

Do not treat the knobs as the bug. They are the fallback working. The bug is whatever
made the face unavailable, and it is upstream of everything in
`PluginControlsViewModel`.
