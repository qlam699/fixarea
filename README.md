# FixArea

A Windows app that keeps **one screen region** reserved: other windows that overlap it are resized or pushed away. You can drag a window onto the region, or use a hotkey to snap the focused app into it.

## Features

- Create **one** locked region by dragging on a selection overlay
- Other windows that overlap the region are automatically **resized** out of the way
- Drag-and-drop onto the region (FancyZones-style) to **pin** and fit the window to the region bounds
- Snap the focused window with a hotkey
- Region overlay: dashed border only, 50% opacity, no fill / no label
- Runs in the background (system tray), optional **Start with Windows**
- Multi-monitor support (coordinates stored per monitor)

## Hotkeys

| Shortcut | Action |
|----------|--------|
| `Ctrl + Alt + R` | Draw / create a lock region (new region replaces the previous one) |
| `Ctrl + Alt + Enter` | Snap the focused window into the lock region |
| `Ctrl + Alt + X` | Delete the lock region |

## Usage

1. Run `FixArea.App.exe`
2. Press `Ctrl + Alt + R`, then drag a rectangle on the screen
3. Drag another app onto the region and release (when the cursor or window edge overlaps it), or focus the app and press `Ctrl + Alt + Enter`
4. Other apps that maximize or overlap the region are resized away from it
5. Closing the main window hides FixArea to the tray (use **Exit** in the tray menu to quit)

## Requirements

- Windows 10 (17763+) / Windows 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) to build

## Build & run

```bash
dotnet build FixArea.sln -c Debug
```

Executable:

```text
src/FixArea.App/bin/x64/Debug/net8.0-windows10.0.19041.0/FixArea.App.exe
```

## Portable release (ZIP)

Do **not** upload the small Debug/Release `.exe` (~268 KB) from `bin/` — that build needs dozens of DLLs next to it and will not run alone.

WinUI 3’s `PublishSingleFile` currently crashes on startup (`ClassFactory cannot supply requested class`). Ship a **self-contained folder as a ZIP** instead:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/publish-portable.ps1
```

Upload:

```text
publish/FixArea-portable-win-x64.zip
```

Users extract the ZIP anywhere and run `FixArea.App.exe` (no .NET install needed). Folder is ~150 MB; the ZIP is smaller due to compression.

Do not enable `PublishTrimmed` — it breaks WinUI.

Config is stored at:

```text
%AppData%\FixArea\config.json
```

## Project structure

```text
FixArea.sln
src/
  FixArea.Core/   # P/Invoke, collision, window hooks, overlay, pin, config
  FixArea.App/    # WinUI 3 (unpackaged), tray, MVVM UI
```

Main stack:

- C# / .NET 8, WinUI 3, Windows App SDK (unpackaged)
- `SetWinEventHook` for window tracking
- `SetWindowPos` / `GetWindowRect` / multi-monitor APIs
- `SHAppBarMessage` for edge-docked regions (when edge criteria are met)

## Limitations

- Games / video players in **exclusive fullscreen** may ignore normal window handling
- **Administrator**-elevated windows may not be controllable unless FixArea is also elevated
- Electron / Chromium apps sometimes create multiple helper HWNDs
- A mid-screen region is not part of the standard Windows work area; FixArea watches and adjusts windows itself

## License

Personal / internal project — no public license attached yet.

Vietnamese docs: [README-vi.md](README-vi.md)
