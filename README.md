# Peak Head Tracking

![PEAK running with this mod](https://raw.githubusercontent.com/itsloopyo/peak-headtracking/main/assets/readme-clip.gif)

An unofficial head tracking mod for PEAK that moves the view with your head while your mouse or controller keeps aiming, driven by a webcam, phone, or any OpenTrack compatible tracker, with no VR headset required.

## Features

- **Decoupled look + aim**: Look around freely with your head while your crosshair stays where you're aiming
- **6DOF head tracking**: Full rotation (yaw, pitch, roll) and positional tracking via OpenTrack UDP protocol
- **Works with any OpenTrack compatible tracker** - free options available for PC, iOS and Android

## Requirements

- [PEAK](https://store.steampowered.com/app/3527290/PEAK/) (Steam)
- [OpenTrack](https://github.com/opentrack/opentrack) or a compatible head tracking app (smartphone, webcam, or dedicated hardware)
- Windows 10/11 (x64)

## Installation

### Lopari

Download [Lopari](https://lopari.app), choose **PEAK**, and click
**Play with head tracking**.

### Standalone Installer

1. Download the latest release from the [Releases page](https://github.com/itsloopyo/peak-headtracking/releases)
2. Extract the ZIP anywhere
3. Double-click `install.cmd`
4. Configure OpenTrack to output UDP to `127.0.0.1:4242`
5. Launch the game

The installer automatically finds your game via Steam registry lookup. If it can't find the game:
- Set the `PEAK_GAME_PATH` environment variable to your game folder, or
- Run from command prompt: `install.cmd "D:\Games\PEAK"`

### Manual Installation

If you prefer to install manually or the installer doesn't work for you:

1. Install [BepInExPack_PEAK](https://thunderstore.io/c/peak/p/BepInEx/BepInExPack_PEAK/) into your game folder:
   - Download and extract the archive
   - Copy the contents of the `BepInExPack_PEAK` folder to your game root (where `Peak.exe` lives) - this includes `winhttp.dll`, `doorstop_config.ini`, and the `BepInEx` folder
2. Download the **Nexus** release ZIP (the one ending in `-nexus.zip`)
3. Extract it into your game folder - the DLLs will land in `BepInEx/plugins/`:
   - `PeakHeadTracking.dll`
   - `CameraUnlock.Core.dll`
   - `CameraUnlock.Core.Unity.dll`
4. Configure your tracker to output UDP to `127.0.0.1:4242`
5. Launch the game

## Setting Up OpenTrack

The mod listens for OpenTrack pose data on UDP port `4242`, on every network
interface. One datagram is six little-endian 64-bit floats in the order
`x, y, z, yaw, pitch, roll`: position in centimetres, rotation in degrees, 48
bytes in total. Anything that sends that to that port drives the view.
OpenTrack's **UDP over network** output sends exactly this, and the steps below
set it up.

1. Install [OpenTrack](https://github.com/opentrack/opentrack/releases).
2. Pick a tracker under **Input**, using the notes below.
3. Set **Output** to **UDP over network**, host `127.0.0.1`, port `4242`.
4. Press **Start**. Tracking and the game can start in either order.

### Webcam

OpenTrack ships a `neuralnet tracker` input that reads a plain webcam. Select it
under **Input**, pick your camera in its settings, and use the output settings
above. How well it tracks depends on your camera and your lighting, so try it
before buying anything.

### Phone

A phone app can reach the mod directly, with no OpenTrack on the PC, if it sends
the datagram described above. Point it at this PC's IP address (run `ipconfig`
to find it) on port `4242`. Not every phone tracker speaks this protocol, so
check yours for an OpenTrack or UDP output option first. [Headcam](https://headcam.app)
sends it, and I wrote it so decent tracking is free for anyone who already owns
a phone.

Sending direct works when the app filters its own signal on the device. The
mod's smoothing is sized to take the edge off a clean signal rather than to
rescue a noisy one, so a raw feed sent direct will jitter. If it does, point the
app at OpenTrack's **UDP over network** *input* on some other port, say 5252,
and let OpenTrack's filters and curves clean it up before its output forwards to
`127.0.0.1:4242`.

Anything arriving from outside `127.0.0.0/8` counts as a remote connection and
is smoothed with `RemoteSmoothing` rather than `LocalSmoothing`. That includes a
tracker on this very PC that sends to the machine's own LAN address, because the
mod reads the source address and not the machine.

### Headset or other hardware

If your device has an OpenTrack input driver, select it under **Input** and use
the same output settings. OpenTrack's own **Input** list is the authority on
what it can read; the mod only ever sees what OpenTrack sends.

### Centring

Centring belongs to your tracker. The mod subtracts no centre of its own: it
applies the pose it receives exactly as it arrives, so a stream of zeros holds
the view where the game itself puts it. Press the centre control in your tracker
(OpenTrack's **Center** bind, or the CENTER button in Headcam) and the tracker
zeroes its own output, which leaves the view centred with the mod doing nothing.

That is why there is no centre hotkey here and nothing to re-centre in game. Two
centres in series would drift apart, because each side re-centres at moments the
other cannot see, and you would end up pressing twice to centre once. If the
view sits off to one side, centre it in the tracker.

## Controls

Two equivalent binding sets - use whichever your keyboard has:

| Action              | Nav-cluster | Chord           |
|---------------------|-------------|-----------------|
| Toggle tracking     | `End`       | `Ctrl+Shift+Y`  |
| Cycle tracking mode | `Page Up`   | `Ctrl+Shift+G`  |
| Toggle yaw mode     | `Page Down` | `Ctrl+Shift+H`  |
| Reload settings     | `F12`       |                 |

There is no recentre key. Your tracker app owns the centre: use its own
control (opentrack's Center bind, the CENTER button in Headcam, SteamVR's
reset) and the mod applies whatever pose it receives.

`Page Up` / `Ctrl+Shift+G` cycles tracking mode:

1. Normal head-tracked gameplay
2. Positional tracking disabled, rotational tracking enabled
3. Rotational tracking disabled, positional tracking enabled
4. Back to normal

`Page Down` / `Ctrl+Shift+H` switches yaw between world-locked (horizon-stable,
the default) and camera-local, which follows the camera's own up axis.

The tracking mode and the yaw mode are saved to `CameraUnlock.ini` as you switch
them, so the game starts in the mode you left it in. `End` is not saved: head
tracking starts on or off as `EnableOnStartup` says.

`F12` reads `CameraUnlock.ini` and `Defaults.ini` again, applies them, and
restarts the UDP listener, without restarting the game.

While head tracking moves the view, the game's crosshair and the interaction
prompts beside it move to where your character is aiming. No setting or key
turns that off.

Every hotkey is a list in the config file, so you can rebind a key, add one or
remove one there. See [Configuration](#configuration).

## Configuration

<!-- cameraunlock:config -->
The mod reads its settings from `BepInEx\config\CameraUnlock.ini` in the game folder, and creates the file when it starts and finds none. Edit it with any text editor.

A setting set to `default` takes its value from `Defaults.ini`, which every head tracking mod that keeps its settings in `CameraUnlock.ini` reads. Head tracking mods that keep their settings in another file do not read it. Writing a value in place of `default` changes that setting for this game only. When the mod saves a setting that a hotkey changed in game, it writes the new value in place of `default`, so that setting no longer follows `Defaults.ini` in this game until you set it to `default` again.

`Defaults.ini` is `%AppData%\CameraUnlock\Defaults.ini` on Windows; `$XDG_CONFIG_HOME/CameraUnlock/Defaults.ini` on Linux, or `~/.config/CameraUnlock/Defaults.ini` where `XDG_CONFIG_HOME` is not set, under Wine and Proton too; and `~/Library/Application Support/CameraUnlock/Defaults.ini` on macOS. The mod's log, where it writes one, names the file it read.

When the mod starts and finds no `Defaults.ini`, it creates one holding the built-in values, unless Windows runs the game as a packaged app, or the game runs on Linux or macOS without Wine or Proton. The mod never changes `Defaults.ini` after that. Edit it with any text editor.

On Linux and macOS without Wine or Proton, this version reads its settings and saves none: it creates no `CameraUnlock.ini` and a change made in game lasts until the game closes.

BepInEx's ConfigurationManager does not list these settings.

The built-in value of each setting set to `default` below:

- `UdpPort=4242`
- `EnableOnStartup=true`
- `WorldSpaceYaw=true`
- `RotationEnabled=true`
- `LocalSmoothing=0.0`
- `RemoteSmoothing=0.15`
- `PositionEnabled=true`
- `PositionLimitX=0.3`
- `PositionLimitY=0.2`
- `PositionLimitYDown=0.2`
- `PositionLimitZ=0.4`
- `PositionLimitZBack=0.1`
- `ToggleKey=End, Ctrl+Shift+Y`
- `CycleTrackingModeKey=PageUp, Ctrl+Shift+G`
- `YawModeKey=PageDown, Ctrl+Shift+H`

With every setting at its default, the file reads:

```ini
; PEAK head tracking settings.
; Comments start with ; and go on their own line. Text after a value is part of the value.
; Hotkeys are key names such as End, PageUp or Ctrl+Shift+Y. Separate several with commas; leave empty for none.
; A setting set to default takes its value from Defaults.ini, which every head tracking mod
; that keeps its settings in CameraUnlock.ini reads: %AppData%\CameraUnlock\Defaults.ini on
; Windows, $XDG_CONFIG_HOME/CameraUnlock/Defaults.ini (normally ~/.config/CameraUnlock) on
; Linux, under Wine and Proton too, and ~/Library/Application Support/CameraUnlock/Defaults.ini
; on macOS. The log names the file it read. Write a value instead of default to change that
; setting for this game only.

[CameraUnlock]
; Written by the mod. Leave this section in place.
ConfigFormat=1

[Network]
; UDP port the mod receives tracker data on (OpenTrack protocol).
UdpPort=default

[General]
; true: head tracking is on when the game starts. ToggleKey turns it on and off.
EnableOnStartup=default
; true: yaw turns around the world's up axis. false: around the camera's own up axis.
WorldSpaceYaw=default
; true: turning your head turns the view.
; Tracking mode at startup, with PositionEnabled. The mode hotkey changes both.
RotationEnabled=default

[Smoothing]
; Smoothing when the tracker runs on this PC. 0 is the least, 1 the most.
LocalSmoothing=default
; Smoothing when the tracker is another device on the network, such as a phone.
; 0 is the least, 1 the most.
RemoteSmoothing=default

[Position]
; true: moving your head moves the view.
; Tracking mode at startup, with RotationEnabled. The mode hotkey changes both.
PositionEnabled=default
; How far, in metres, leaning left or right can move the view.
PositionLimitX=default
; How far, in metres, raising your head can move the view.
PositionLimitY=default
; How far, in metres, lowering your head can move the view.
PositionLimitYDown=default
; How far, in metres, leaning forward can move the view.
PositionLimitZ=default
; How far, in metres, leaning back can move the view.
PositionLimitZBack=default

[Hotkeys]
; Turns head tracking on and off.
ToggleKey=default
; Changes the tracking mode: rotation and position, rotation only, position only.
CycleTrackingModeKey=default
; Switches yaw between the world's up axis and the camera's own (WorldSpaceYaw).
YawModeKey=default
; Reads this file and Defaults.ini again and applies them, without restarting
; the game.
ReloadConfigKey=F12

[Camera]
; Metres. While head tracking moves the view, the camera's near clip plane is
; held at least this far out, so the view does not see through your character's
; model.
MinNearClip=0.15

[Logging]
; true: write the head tracking state to the BepInEx log every 120 frames,
; at the Debug level.
DebugLogging=false
```
<!-- /cameraunlock:config -->

## Troubleshooting

**Game crashes on startup after installing BepInEx:**
- PEAK requires the [BepInExPack_PEAK](https://thunderstore.io/c/peak/p/BepInEx/BepInExPack_PEAK/) build (ships with a PEAK-specific doorstop). A copy is bundled inside the release ZIP and `install.cmd` extracts it from there, so the installer never reaches out to the network.
- If the game crashes on startup, add `-force-vulkan` to your Steam launch options (game Properties > General > Launch Options) to bypass DX12

**Mod not loading:**
- Check `BepInEx/LogOutput.log` for errors
- Ensure all three DLLs are in `BepInEx/plugins/`: `PeakHeadTracking.dll`, `CameraUnlock.Core.dll`, `CameraUnlock.Core.Unity.dll`
- Verify `winhttp.dll` is in the game folder

**No tracking response:**
- Verify OpenTrack is running and outputting data
- Check UDP port matches (default 4242)
- Press **End** to enable tracking
- If the view sits off to one side, centre it in your tracker app. The mod keeps no centre of its own
- Check firewall isn't blocking UDP port 4242

**A config edit had no effect:**
- Make sure nothing follows the value on the line. A comment goes on its own line: a trailing `; comment` is read as part of the value, the setting keeps its default, and `BepInEx/LogOutput.log` names the line and the value it could not read.
- Edits to `CameraUnlock.ini` take effect at the next start, or when you press `F12`.

**Jittery movement:**
- Increase `RemoteSmoothing` for a phone or other network device, or `LocalSmoothing` for a tracker running on this PC. Both smooth rotation and position
- Deadzones are set in your tracker app, not in this mod
- Improve lighting for webcam-based tracking

**Yaw feels wrong when looking up or down at extreme angles:**
- Try toggling between world-locked and camera-local yaw with `Page Down`. World-locked (default) is horizon-stable; camera-local follows the camera's current up-axis.

## Updating

Download the new release and run `install.cmd` again.

## Uninstalling

Run `uninstall.cmd` from the release folder. This removes the mod DLLs. BepInEx is only removed if it was originally installed by this mod. To force-remove BepInEx:

```
uninstall.cmd /force
```

## Building from Source

### Prerequisites

- [.NET SDK](https://dotnet.microsoft.com/download) (any recent version)
- [pixi](https://pixi.sh) task runner
- PEAK installed only for `pixi run install` and the optional `pixi run setup-game` / `pixi run verify-refs` checks. The build uses the vendored BepInEx and core's Unity reference stubs.

### Build

```bash
git clone --recurse-submodules https://github.com/itsloopyo/peak-headtracking.git
cd peak-headtracking

# Build and install to game
pixi run install

# Build only
pixi run build

# Package for release
pixi run package
```

### Available Tasks

| Task | Description |
|------|-------------|
| `pixi run build` | Build the mod (Release configuration) |
| `pixi run install` | Build and install to game directory |
| `pixi run uninstall` | Remove the mod from the game |
| `pixi run uninstall -- --force` | Remove the mod and BepInEx |
| `pixi run test` | Build and run the tests, the config differential test included |
| `pixi run package` | Run the tests and create release ZIPs |
| `pixi run render-config` | Rewrite `config/CameraUnlock.ini` from the config table |
| `pixi run validate-manifest` | Check the built ZIPs against the launcher manifest contract |
| `pixi run clean` | Clean build artifacts |
| `pixi run release` | Version bump, build, tag, and push |

## Community & Support

- Discord: [Loop's Head Tracking Hangout](https://discord.com/invite/dxyZdyFNT9) - setup help, bug reports, and new-release announcements
- [Lopari](https://lopari.app) - free Windows launcher with one-click install and launch for the released head-tracking mods
- [Headcam](https://headcam.app) - free app that turns your iPhone or Android phone into the head tracker

## License

MIT License - see [LICENSE](LICENSE) for details.

The MIT licence covers the code in this repository. It does not cover the
bundled third-party components, or the PEAK gameplay footage in `assets/` (the
clip at the top of this page and the package icon), which belongs to the game's
developers and publishers. See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)
for the full breakdown.

## Credits

- [Aggro Crab](https://aggrocrab.com/) / [Landfall](https://landfall.se/) - PEAK
- [BepInEx](https://github.com/BepInEx/BepInEx) - Unity modding framework
- [OpenTrack](https://github.com/opentrack/opentrack) - Head tracking software
- [HarmonyX](https://github.com/BepInEx/HarmonyX) - Runtime patching library, the BepInEx fork of Andreas Pardeike's [Harmony](https://github.com/pardeike/Harmony)

## Disclaimer

This mod is not affiliated with, endorsed by, or supported by Aggro Crab Games or Landfall. Use at your own risk.
