<p align="center">
  <img src="Assets/musix.png" width="120" alt="MusiX icon">
</p>

<h1 align="center">MusiX</h1>

<p align="center">
  A lightweight, dark-themed local music player for Windows.<br>
  <a href="README-ind.md">🇮🇩 Baca dalam Bahasa Indonesia</a>
</p>

---

## Features

- **Frameless window** with a custom title bar, resizable from every edge, and Windows **Snap** support (drag to screen edges or use `Win + Arrow`).
- **Open a folder** (scanned recursively) or **drag & drop** files and folders straight into the window.
- **Playlist** with title, artist and duration read from file tags, duplicates skipped automatically.
- **Playback controls:** play / pause, previous, next, stop, seek bar, volume.
- **Shuffle** and **repeat** (off / all / one).
- **Remembers your session:** playlist, last track, volume, shuffle and repeat settings are restored on the next launch.
- Missing files are highlighted in the list instead of crashing the player.

Supported extensions: `.mp3` `.wav` `.wma` `.m4a` `.aac` `.flac` `.ogg` `.aiff`
(playback of some formats depends on the codecs available in your Windows installation).

## Keyboard shortcuts

| Key | Action |
| --- | --- |
| `Space` | Play / pause |
| `←` / `→` | Seek −5 s / +5 s |
| `↑` / `↓` | Volume +5 / −5 |
| `M` | Mute / unmute |
| `N` / `P` | Next / previous track |
| `S` | Stop |
| `Enter` or double-click | Play the selected track |
| `Delete` | Remove the selected track from the playlist |

## Requirements

- **Windows 10 or 11** (Windows only, see [Platform support](#platform-support))
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) to build from source
- An internet connection on the **first launch** (UI icons are downloaded once, then cached)

## Build & run

```bash
git clone https://github.com/Andrew-466/Musix.git
cd Musix
dotnet run
```

### Publish as a single `.exe`

```bash
dotnet publish -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true
```

The result is in `bin/Release/net8.0-windows/win-x64/publish/MusicPlayer.exe` and runs on any Windows PC without installing .NET.
(In PowerShell, replace `^` with a backtick `` ` `` or put the command on one line.)

> Do not enable `PublishTrimmed`; Windows Forms does not support trimming reliably.

## Project structure

| File | Purpose |
| --- | --- |
| `Program.cs` | Application entry point |
| `Form1.cs` | Main window, UI layout, playback logic, playlist, settings |
| `WindowChrome.cs` | Frameless-window helpers: title bar buttons and resize edges |
| `AccentSlider.cs` | Custom seek / volume slider |
| `RoundButton.cs` | Custom rounded button |
| `BxIcon.cs` | Loads and tints [Boxicons](https://boxicons.com) SVGs |
| `Assets/` | App icon (`musix.ico`, `musix.png`) |

## Where data is stored

| Data | Location |
| --- | --- |
| Settings and last playlist | `%AppData%\MusicPlayer\settings.json` |
| Cached UI icons | `%LocalAppData%\MusicPlayer\icons` |

Your music files are never copied or modified; only their paths are saved.

## Platform support

MusiX is **Windows only**. It relies on Windows Forms, NAudio's Windows audio output and a few Win32 APIs (`user32`, `dwmapi`, `uxtheme`). A cross-platform version would need a different UI toolkit (for example Avalonia) and audio backend.

## Built with

- [.NET 8](https://dotnet.microsoft.com/) and Windows Forms
- [NAudio](https://github.com/naudio/NAudio): audio playback
- [TagLibSharp](https://github.com/mono/taglib-sharp): reading tags and duration
- [Svg.Skia](https://github.com/wieslawsoltes/Svg.Skia): rendering SVG icons
- [Boxicons](https://boxicons.com): icon set
