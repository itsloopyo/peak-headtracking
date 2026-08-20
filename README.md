# Peak Head Tracking

![Mod GIF](https://raw.githubusercontent.com/itsloopyo/peak-headtracking/main/assets/readme-clip.gif)

An **unofficial** BepInEx mod that adds head tracking to PEAK via OpenTrack. Look around naturally with your head while your aim stays independent.

## Features

- **Decoupled look + aim**: Look around freely with your head while your crosshair stays where you're aiming
- **6DOF head tracking**: Full rotation (yaw, pitch, roll) and positional tracking via OpenTrack UDP protocol

## Requirements

- [PEAK](https://store.steampowered.com/app/3527290/PEAK/) (Steam)
- [OpenTrack](https://github.com/opentrack/opentrack) or a compatible head tracking app (smartphone, webcam, or dedicated hardware)
- Windows 10/11 (x64)

## Installation

1. Download the latest release from the [Releases page](https://github.com/itsloopyo/peak-headtracking/releases)
2. Extract the ZIP anywhere
3. Double-click `install.cmd`
4. Configure OpenTrack to output UDP to `127.0.0.1:4242`
5. Launch the game

The installer automatically finds your game via Steam registry lookup. If it can't find the game:
- Set the `PEAK_PATH` environment variable to your game folder, or
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

1. Download and install [OpenTrack](https://github.com/opentrack/opentrack/releases)
2. Configure your tracker as input
3. Set output to **UDP over network**
4. Host: `127.0.0.1`, Port: `4242`
5. Start tracking before launching the game

### Webcam Setup

No special hardware needed - OpenTrack's built-in **neuralnet tracker** uses any webcam for 6DOF face tracking.

1. In OpenTrack, set the input to **neuralnet tracker**
2. Select your webcam in the tracker settings
3. Set output to **UDP over network** (`127.0.0.1:4242`)
4. Start tracking before launching the game
5. Centre with OpenTrack's own Center hotkey whenever you need to; the mod applies whatever pose OpenTrack sends

### Phone App Setup

This mod includes built-in smoothing for network jitter on positional tracking (`Remote Smoothing`, default 0.15), so you can send directly from your phone on port 4242 without needing OpenTrack on PC. Rotation is not smoothed by that setting; it is interpolated between tracker samples by the PoseInterpolator, which also absorbs uneven packet timing.

1. Install an OpenTrack-compatible head tracking app
2. Configure it to send to your PC's IP on port 4242 (run `ipconfig` to find it)
3. Set the protocol to OpenTrack/UDP

**With OpenTrack (optional):** If you want curve mapping or visual preview, route through OpenTrack. Set OpenTrack's input to "UDP over network" on a different port (e.g. 5252), output to `127.0.0.1:4242`, and point your phone at port 5252. Make sure your firewall allows incoming UDP on the input port.

## Controls

Two equivalent binding sets - use whichever your keyboard has:

| Action              | Nav-cluster | Chord           |
|---------------------|-------------|-----------------|
| Toggle tracking     | `End`       | `Ctrl+Shift+Y`  |
| Cycle tracking mode | `Page Up`   | `Ctrl+Shift+G`  |
| Toggle yaw mode     | `Page Down` | `Ctrl+Shift+H`  |

There is no recentre key. Your tracker app owns the centre: use its own
control (opentrack's Center bind, the CENTER button in Headcam, SteamVR's
reset) and the mod applies whatever pose it receives.

`Page Up` / `Ctrl+Shift+G` cycles tracking mode:

1. Normal head-tracked gameplay
2. Positional tracking disabled, rotational tracking enabled
3. Rotational tracking disabled, positional tracking enabled
4. Back to normal

## Configuration

The mod creates a config file at `BepInEx/config/com.cameraunlock.peak.headtracking.cfg` on first run.

The section headers are numbered (`[01. Connection]`, `[02. General]`, and so
on) so they sort in a sensible order. The numbers are part of the section name:
a setting written under `[Connection]` instead of `[01. Connection]` is in a
section the mod never looks at, and it silently keeps its default.

A comment has to sit on its own line. BepInEx splits each line at the first `=`
and takes everything after it as the value, so a trailing `# note` becomes part
of the value, the conversion fails, and the entry silently keeps its default -
the only trace is a line in `BepInEx/LogOutput.log`. Put explanations above the
key, never after it.

```ini
[01. Connection]
# Must match OpenTrack output port (1024-65535)
UDP Port = 4242
# Seconds before reconnection attempt (1-60)
Reconnect Timeout = 5
# Max packets to buffer (10-500)
Packet Buffer Size = 100

[02. General]
# Start with tracking enabled
Tracking Enabled = true
# Enable lean/positional tracking (6DOF)
Position Enabled = true
# Play sounds for tracking state changes
Enable Audio Feedback = true
# Move the crosshair to the real aim point while head tracking is active
Show Reticle = true
# true = horizon-locked yaw (default), false = camera-local yaw
World Space Yaw = true

[03. Sensitivity]
# Horizontal rotation (0.1-5.0)
Yaw Sensitivity = 1.0
# Vertical rotation (0.1-5.0)
Pitch Sensitivity = 1.0
# Head tilt (0.1-5.0)
Roll Sensitivity = 1.0
Invert Yaw = false
Invert Pitch = false
Invert Roll = true
# Lateral sensitivity (0.0-5.0)
Position Sensitivity X = 2.0
# Vertical sensitivity (0.0-5.0)
Position Sensitivity Y = 2.0
# Depth sensitivity (0.0-5.0)
Position Sensitivity Z = 2.0
# Max lateral offset in meters (0.01-0.5)
Position Limit X = 0.30
# Max vertical offset in meters (0.01-0.5)
Position Limit Y = 0.20
# Max forward offset in meters (0.01-0.5)
Position Limit Z = 0.40
# Max backward offset in meters (0.01-0.5)
Position Limit Z Back = 0.10

[04. Rotation Limits]
# Clamp pitch rotation
Enable Pitch Limits = true
# Max look-down angle (-90 to 0)
Minimum Pitch = -85
# Max look-up angle (0 to 90)
Maximum Pitch = 85
# Enable head tilt
Enable Roll = true
# Clamp roll rotation
Enable Roll Limits = true
# Max tilt angle (0-90)
Maximum Roll = 30

[05. Smoothing]
# Both values apply to positional tracking only. Rotation is not smoothed by them:
# the rotation path skips the smoothing stage and relies on the PoseInterpolator,
# which interpolates between tracker samples to fill in frames.
# Tracker on this machine (loopback), 0 = none, 1 = heavy
Local Smoothing = 0.0
# Tracker on a remote network device, 0 = none, 1 = heavy
Remote Smoothing = 0.15

[06. Deadzone]
# Ignore small movements near center
Enable Deadzone = false
# Yaw deadzone in degrees (0-10)
Yaw Deadzone = 0
# Pitch deadzone in degrees (0-10)
Pitch Deadzone = 0
# Roll deadzone in degrees (0-10)
Roll Deadzone = 0

[07. Hotkeys]
Toggle Tracking = End
Toggle Position = PageUp
Yaw Mode Key = PageDown
# Re-read the config file and restart the UDP listener. Only UDP Port takes
# effect this way; sensitivity, limits, deadzones and smoothing are read once at
# startup and need a game restart
Reload Config = F12
# Present in the file but not wired up in this version; changing it does nothing
Toggle Reticle = Insert

[08. Advanced]
# Enable detailed debug logging
Debug Logging = false
# Target update rate in Hz (30-120)
Update Rate = 60
Maintain Relative Position = true
# Prevents seeing through player model during head bob (0.01-0.5)
Near Clip Override = 0.15
```

## Troubleshooting

**Game crashes on startup after installing BepInEx:**
- PEAK requires the [BepInExPack_PEAK](https://thunderstore.io/c/peak/p/BepInEx/BepInExPack_PEAK/) build (ships with a PEAK-specific doorstop). Our `install.cmd` downloads this automatically.
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
- Make sure nothing follows the value on the line. A trailing `# comment` is read as part of the value, the entry falls back to its default, and the game gives no sign of it. `BepInEx/LogOutput.log` records the failed conversion.

**Jittery movement:**
- For jittery leaning (positional tracking), increase `Remote Smoothing` for a phone or other network device, or `Local Smoothing` for a tracker running on this PC, with nothing after the value on the line. These two values do not affect rotation jitter
- Enable deadzones in the `[06. Deadzone]` section
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
- PEAK installed (for Unity/BepInEx DLL references)

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
| `pixi run package` | Create release ZIPs |
| `pixi run clean` | Clean build artifacts |
| `pixi run release` | Version bump, build, tag, and push |

## Community & Support

- Discord: [Loop's Head Tracking Hangout](https://discord.com/invite/dxyZdyFNT9) - setup help, bug reports, and new-release announcements
- [Lopari](https://lopari.app) - free Windows launcher with one-click install and launch for the released head-tracking mods
- [Headcam](https://headcam.app) - free app that turns your iPhone or Android phone into the head tracker

## License

MIT License - see [LICENSE](LICENSE) for details.

## Credits

- [Aggro Crab](https://aggrocrab.com/) / [Landfall](https://landfall.se/) - PEAK
- [BepInEx](https://github.com/BepInEx/BepInEx) - Unity modding framework
- [OpenTrack](https://github.com/opentrack/opentrack) - Head tracking software
- [Harmony](https://github.com/pardeike/Harmony) - Runtime patching library

## Disclaimer

This mod is not affiliated with, endorsed by, or supported by Aggro Crab Games or Landfall. Use at your own risk.
