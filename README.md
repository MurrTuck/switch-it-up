# Switch it up

A small Windows app that flips a monitor between two inputs (for example DisplayPort 1 and HDMI 1) with one click.

It talks to the monitor over DDC/CI, the same channel the monitor's own on-screen menu settings can be changed through, so no extra software or drivers are needed.

## Using it

1. Download `Switch it up.exe` from the latest release, or build it yourself (see below).
2. Open it, choose the monitor, and pick Input A and Input B.
3. Press **Switch** (or hit Enter). The button shows which input it will switch to next.

Your choices are remembered in `%APPDATA%\MonitorSwitch\settings.txt`.

Running `"Switch it up.exe" --toggle` switches the saved monitor straight away without opening the window, which is handy for a hotkey or shortcut.

### If it doesn't work

- Turn on **DDC/CI** in the monitor's on-screen menu.
- Some monitors use their own input numbers. The input boxes accept a number directly (for example `17`) if the named options don't work.
- Switching back only works if the PC can still talk to the monitor on the other input. Some monitors stop listening on the cable that's no longer active.
- Keep the app window on a different monitor than the one being switched, or you won't be able to see the button after switching.

## Building

Needs only Windows. The build uses the C# compiler that comes with the .NET Framework.

```
powershell -ExecutionPolicy Bypass -File make-icon.ps1
build.cmd
```

`make-icon.ps1` draws `app.ico`; it's already in the repo, so you only need it if you change the icon.

## Files

- `MonitorSwitch.cs`: monitor detection, DDC/CI input switching, settings, and startup
- `Ui.cs`: the window and dark theme
- `make-icon.ps1`: draws the app icon
- `build.cmd`: builds `Switch it up.exe`
