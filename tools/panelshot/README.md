# panelshot

Draws a machine's or an effect's panel to a PNG, with no window and no screen, so a layout can
be looked at without starting the application and loading a song.

```bash
cd tools/panelshot
dotnet run -- ../../rack/machines/Ouroboros /tmp/ouroboros.png        # its natural size
dotnet run -- ../../rack/effects/EchoBox /tmp/echobox.png 900         # forced to 900 wide
```

With no width the window is as wide as the panel wants to be, which is how the application sizes
it, so the picture says how much room the layout actually takes.

It draws the real thing: the same `PanelView`, the same readers off disc and the same theme. What
it cannot draw is anything that belongs to a live device rather than to the panel, such as the
wave in a scope or the picture of a take, since nothing is playing. Those come out as the empty
boxes they are before anything is loaded.

Not in the solution on purpose: it is a tool built when it is wanted, and it references the
application, so building it from the solution would build the application twice.
