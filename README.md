# Switch it up

A small Windows app that flips a monitor between two inputs (for example DisplayPort 1 and HDMI 1) with one click.

It talks to the monitor over DDC/CI, the same channel the monitor's own on-screen menu settings can be changed through, so no extra software or drivers are needed.

## Using it

1. Download `Switch it up.exe` from the latest release, or build it yourself (see below).
2. Open it, choose the monitor, and pick Input A and Input B.
3. Press **Switch** (or hit Enter). The button shows which input it will switch to next.

**Keyboard shortcut:** while the app is open, **Ctrl + Alt + S** switches from anywhere. To change it, click the shortcut box and press a new combination (it needs Ctrl or Alt); Backspace turns it off.

If the window is on the monitor being switched, it moves itself to the nearest other screen first, so the button stays in view.

Your choices are remembered in `%APPDATA%\MonitorSwitch\settings.txt`.

Running `"Switch it up.exe" --toggle` switches the saved monitor straight away without opening the window, which is handy for a hotkey or shortcut.

### If it doesn't work

- Turn on **DDC/CI** in the monitor's on-screen menu.
- Some monitors use their own input numbers. The input boxes accept a number directly (for example `17`) if the named options don't work.
- Switching back only works if the PC can still talk to the monitor on the other input. Some monitors stop listening on the cable that's no longer active.

## Building

Needs only Windows. The build uses the C# compiler that comes with the .NET Framework.

```
build.cmd
```

`make-icon.ps1` draws `app.ico`; it's already in the repo, so you only need it if you change the icon.

### Releasing

`release.ps1` builds the app and installs it to `%LOCALAPPDATA%\Programs\Switch it up` (closing and reopening it if it's running). With a version number it also pushes to GitHub and publishes a release with the exe attached:

```
powershell -ExecutionPolicy Bypass -File release.ps1                 # build + install
powershell -ExecutionPolicy Bypass -File release.ps1 -Version 1.1    # also publish v1.1
```

Commit your changes first; it refuses to publish with uncommitted changes or a version that already exists.

## Files

- `MonitorSwitch.cs`: monitor detection, DDC/CI input switching, settings, and startup
- `Ui.cs`: the window and dark theme
- `make-icon.ps1`: draws the app icon
- `build.cmd`: builds `Switch it up.exe`
- `release.ps1`: builds, installs, and optionally publishes a GitHub release
