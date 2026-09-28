# Changelog

## [Unreleased]

### Changed

- Settings move to `BepInEx\config\CameraUnlock.ini`. Earlier versions of the mod kept these settings in `com.cameraunlock.peak.headtracking.cfg`, in the same folder. The first time this version starts and finds no `CameraUnlock.ini`, it reads your settings from `com.cameraunlock.peak.headtracking.cfg` and writes them into `CameraUnlock.ini`. It never changes `com.cameraunlock.peak.headtracking.cfg`, and does not read it again while `CameraUnlock.ini` exists.
- BepInEx's ConfigurationManager no longer lists these settings. Edit `BepInEx\config\CameraUnlock.ini` with any text editor.
- A setting that the defaults the README shows set to `default` is written as `default` when you never changed it from the default earlier versions used, because `com.cameraunlock.peak.headtracking.cfg` does not hold it or holds that default. It then follows `Defaults.ini`, so it takes the value `Defaults.ini` gives it, or the built-in value where `Defaults.ini` gives none, which can differ from the default earlier versions used. A setting you changed is written with the value imported for it, or as `default` where that value equals its default at that start.
- `RotationEnabled` and `PositionEnabled` are one setting here, the tracking mode, so both are written as `default` or neither is.
- Comments, and keys the mod never read, are not carried over. Nor are these, where your old file had them:
  - A sensitivity, scale, deadzone, response curve or axis inversion you changed from its default. Set these in your tracker instead.
  - Reticle settings, and a key that toggled the reticle.
- An older version of the mod reads `com.cameraunlock.peak.headtracking.cfg` and never reads `CameraUnlock.ini`, so a setting you change after updating is not in `com.cameraunlock.peak.headtracking.cfg`.
- Deleting only `CameraUnlock.ini` makes the next start read `com.cameraunlock.peak.headtracking.cfg` again. To go back to the defaults, replace everything in `CameraUnlock.ini` with the defaults the README shows. Every setting they set to `default` then follows `Defaults.ini`.
- On Linux and macOS without Wine or Proton, this version reads its settings and saves none: it creates no `CameraUnlock.ini`, reads your settings from `com.cameraunlock.peak.headtracking.cfg` again at every start while there is no `CameraUnlock.ini`, and a change made in game lasts until the game closes.
- Setting names follow the other CameraUnlock mods: `UDP Port` is `UdpPort`, `Tracking Enabled` is `EnableOnStartup`, `World Space Yaw` is `WorldSpaceYaw`, `Local Smoothing` and `Remote Smoothing` are `LocalSmoothing` and `RemoteSmoothing`, and `Position Limit X`, `Y`, `Z` and `Z Back` are `PositionLimitX`, `PositionLimitY`, `PositionLimitZ` and `PositionLimitZBack` under `[Position]`. `PositionLimitYDown` is new and sets how far lowering your head moves the view, which `Position Limit Y` used to set as well; your old value is imported into both. `Near Clip Override` is `[Camera] MinNearClip`, `Debug Logging` is `[Logging] DebugLogging` and `Reload Config` is `[Hotkeys] ReloadConfigKey`.
- Hotkeys are written as key names, and each hotkey lists every key that triggers it, the Ctrl+Shift chord included: `ToggleKey=End, Ctrl+Shift+Y`. The chords can be rebound or removed like any other key. `Toggle Tracking`, `Toggle Position` and `Yaw Mode Key` are `ToggleKey`, `CycleTrackingModeKey` and `YawModeKey`, and your key is imported with its chord beside it.
- A hotkey bound to a plain key no longer fires while Ctrl and Shift are both held, so Ctrl+Shift with that key reaches only a binding that names the chord.
- Turning head tracking on or off with End no longer changes the file. The mod starts with head tracking on or off as `EnableOnStartup` says.
- The tracking mode (`Page Up`) is saved as the `RotationEnabled` and `PositionEnabled` pair, so the game starts in the mode you left it in, position only included. Earlier versions saved only whether position was on, and always started with rotation on. The yaw mode (`Page Down`) is saved as `WorldSpaceYaw`, as before.
- `F12` reads `CameraUnlock.ini` and `Defaults.ini` again and applies every setting, then restarts the UDP listener. In earlier versions it closed the listener for good, so head tracking stopped until the game was restarted.
- If your `com.cameraunlock.peak.headtracking.cfg` was first written by v1.0.0 to v1.1.1, it holds `Invert Roll = false`, the default those versions shipped. v1.1.2 made `true` the default (79eaef0), and BepInEx kept the value already in your file. That value is not carried over: roll now follows the `true` default, as it does for everyone who installed later, and the log names `Invert Roll=false` as not carried. If tilting your head now tilts the view the wrong way, invert roll in your tracker.
- `LocalSmoothing` and `RemoteSmoothing` now smooth rotation as well as position (b3e2cca). v1.3.0 applied them to position only. A tracker on another device on the network, such as a phone, now gets `RemoteSmoothing` (0.15 by default) on rotation, where v1.3.0 applied none. Setting `RemoteSmoothing` to 0 gives rotation as v1.3.0 had it, and takes the smoothing off position as well. A tracker on this PC is unchanged at the `LocalSmoothing` default of 0.
- The `Toggle Reticle` key (`Insert` / `Ctrl+Shift+U`), which v1.3.0 read and never acted on, was wired up after v1.3.0 (b3e2cca). This version removes it again, so, as in v1.3.0, no key turns the crosshair off.
- A hotkey set to Ctrl, Shift or Alt on its own in `com.cameraunlock.peak.headtracking.cfg` is not carried over. That key goes down before the key of any chord made with it, so the hotkey is left unbound, it keeps its Ctrl+Shift chord, and the log names the key as not carried.
- A hotkey set to a number that is not a key code Unity names in `com.cameraunlock.peak.headtracking.cfg` (for example `Toggle Position = 2`) is not carried over. The hotkey is left unbound, it keeps its Ctrl+Shift chord where it has one, and the log names the number as not carried.

### Added

- A setting set to `default` in `CameraUnlock.ini` takes its value from `Defaults.ini`, which every head tracking mod that keeps its settings in `CameraUnlock.ini` reads. Head tracking mods that keep their settings in another file do not read it, and neither do earlier versions of this mod. Writing a value in place of `default` changes that setting for this game only. When the mod saves a setting that a hotkey changed in game, it writes the new value in place of `default`, so that setting no longer follows `Defaults.ini` in this game until you set it to `default` again.
- `Defaults.ini` is `%AppData%\CameraUnlock\Defaults.ini` on Windows; `$XDG_CONFIG_HOME/CameraUnlock/Defaults.ini` on Linux, or `~/.config/CameraUnlock/Defaults.ini` where `XDG_CONFIG_HOME` is not set, under Wine and Proton too; and `~/Library/Application Support/CameraUnlock/Defaults.ini` on macOS. The mod's log, where it writes one, names the file it read.
- When the mod starts and finds no `Defaults.ini`, it creates one holding the built-in values, unless Windows runs the game as a packaged app, or the game runs on Linux or macOS without Wine or Proton. The mod never changes `Defaults.ini` after that.

### Removed

- The key that toggled the reticle (`Insert` / `Ctrl+Shift+U`), and the `Show Reticle` setting. The crosshair and the interaction prompts always follow your aim while head tracking moves the view.
- The sensitivity, deadzone and axis inversion settings (`Yaw Sensitivity`, `Pitch Sensitivity`, `Roll Sensitivity`, `Invert Yaw`, `Invert Pitch`, `Invert Roll`, `Position Sensitivity X`, `Y` and `Z`, `Enable Deadzone`, `Yaw Deadzone`, `Pitch Deadzone` and `Roll Deadzone`). Set these in your tracker app instead. With them at the defaults v1.1.2 to v1.3.0 shipped, the camera moves as it did before: the position multiplier of 2 and the inverted roll are built into the mod now.
- The settings earlier versions read and never used: `Reconnect Timeout`, `Packet Buffer Size`, `Enable Audio Feedback`, `Enable Pitch Limits`, `Minimum Pitch`, `Maximum Pitch`, `Enable Roll`, `Enable Roll Limits`, `Maximum Roll`, `Update Rate` and `Maintain Relative Position`. Changing them had no effect.

## [1.3.0] - 2026-08-20

### Added

- drop mod-side centring, the tracker app owns the centre
- Log the UDP port at startup and a one-shot `Tracker data received` line the
  first time packets arrive, so a "no head tracking" report is answerable from
  `BepInEx/LogOutput.log` alone. The port previously went out at debug level,
  which BepInEx does not write to disk by default.

### Changed

- The mod no longer keeps a centre at all. The tracker app owns centring, so a
  mod-side centre was a second centre in series with the tracker's own and the
  two drifted apart. Centre in your tracker app instead (opentrack's Center
  bind, the CENTER button in Headcam, SteamVR's reset); the mod applies the pose
  it receives as absolute. The `Home` key, the `Ctrl+Shift+T` chord and the
  `Recenter View` config entry are gone.

### Fixed

- harden the release catalog pin sync and tag interpolation

## [1.2.1] - 2026-08-18

### Changed

- Replace the single `Smoothing` config key with `Local Smoothing` (default 0.0) and `Remote Smoothing` (default 0.15), selected per connection from the packet source address. Both apply to positional tracking only; rotation is not smoothed by them, because the rotation path skips the smoothing stage and gets its smoothness from the PoseInterpolator
- Remove the `Position Smoothing` key: position now uses the connection-selected `Local Smoothing` / `Remote Smoothing` value
- Remove the hidden 0.15 baseline smoothing floor. `Local Smoothing` 0.0 now adds no
  user smoothing of its own; what is left is the always-on frame interpolation, a flat
  20 ms time constant that keeps a low-rate tracker smooth on a high-refresh display

### Fixed

- migrate to the per-connection smoothing pair
- match stub member kinds to the shipped Unity assemblies

## [1.2.0] - 2026-08-03

### Fixed

- show full control set in pixi install via shared -Controls
- recenter from tracker app requests, not on data resume

### Other

- Link Discord, Lopari and Headcam from the README

## [1.1.2] - 2026-06-07

### Added

- add HeadTrackingSession and expand C++ core with RE Engine, Unreal, and tracking-session modules
- add typed ForStaticProperty<T> and ForInstanceField<T> to CompiledGetters
- aim projection, reframework/unreal hooks, input/logging hardening, games
- add Mass Effect Legendary Edition to games catalog
- expand games catalog, fix unicode games.json read, stage launcher manifest
- add Pacific Drive to games catalog
- add Homeworld: Remastered Collection to games catalog
- add manifest-mode installer validator and ASI loader subdir support
- authenticate GitHub API requests via env token when present
- add R.E.P.O. detection data

### Fixed

- fail fast in ASI dev-deploy when the game is running
- remove unused profile system that reverted user config edits on launch
- restore il2cpp camera position by undoing applied local delta
- set SO_REUSEADDR so the receiver reclaims its port on relaunch
- harden release.ps1 - changelog gate before version bump, add -Force
- default Invert Roll to true
- sync fog quad to head-tracked pose (6DOF position + near clip)

### Other

- Add Ubisoft Connect detection and VendorZip BepInEx install
- Add PluginSubfolder param to Invoke-DevDeployBepInEx
- Add Xbox install path for Easy Delivery Co
- Add GOG IDs for Cyberpunk 2077
- Add PLUGIN_SUBFOLDER support to BepInEx install/uninstall bodies
- scripts: drop the two-phase loader-init prompt from install bodies
- data: add Black & White (Lionhead) to games registry
- scripts: detect BepInEx 6 IL2CPP via BepInEx.Core.dll marker
- powershell: skip cameraunlock-core remote refresh in CI
- scripts: add UE4SS install template, fix delayed expansion in ASI body, expand games registry
- protocol: reject finite-but-out-of-float-range packet values
- data: add Subnautica 2 to games registry
- detection: add installer-registry game path lookup (Black & White GameDir)
- protocol: reorder tracking data member in udp_receiver
- data: fix Subnautica 2 Steam app id (3367150 -> 1962700)
- data: add Ni no Kuni Remastered and Yakuza 0; switch find-game output to UTF-8
- detection: add Xbox/GDK build support for Subnautica 2 (and any future GDK title)
- find-game: escape `&` in GAME_DISPLAY_NAME so echo doesn't split
- templates: add uninstall.ps1; data: add Deus Ex Mankind Divided
- powershell: add NightlyRelease module for Patreon-gated nightly builds
- protocol: disable SIO_UDP_CONNRESET and add one-shot receiver diagnostics; powershell: write nightly manifest.json without UTF-8 BOM; data: add Mixtape
- powershell: stop redirecting git stderr in Update-CameraUnlockCoreToRemoteTip
- powershell: publish dev builds as GitHub pre-releases
- protocol: disable SIO_UDP_CONNRESET and add one-shot receiver diagnostics
- data: add Mixtape
- powershell: stop redirecting git stderr in Update-CameraUnlockCoreToRemoteTip
- powershell: run gh under Continue so its stderr doesn't abort the dev-release publish
- reframework: strip VR runtime DLLs on install for flatscreen mode
- reframework: cache GetValue method and avoid per-call heap in ArrayGetValue; data: add BioShock Infinite
- uninstall: remove reframework_revision.txt marker dropped at game root
- install: render MOD_CONTROLS multi-line via percent expansion
- Add YAPYAP to games.json
- powershell: write state file BOM-less so Lopari JSON parser accepts it
- powershell: stop redirecting git stderr in Invoke-VersionCommit

## [1.1.1] - 2026-05-03

### Other

- Add DX11 overlay header for crosshair rendering
- Update PositionInterpolator tests for bounded extrapolation
- Skip vendor refresh when SHA-256 matches existing copy
- Fix degenerate-input bugs in scanners, projection, and color parser
- Add yaw-mode key and WorldSpaceYaw config options
- Quote /y flag detection and add shared install/uninstall bodies
- Add DevDeploy module with Cecil dev-install orchestrator
- Auto-refresh cameraunlock-core submodule in Copy-SharedBundle
- Add install bodies and dev-deploy orchestrators for non-Cecil frameworks
- Thin install/uninstall/deploy scripts to shared bodies
- Resolve exe relpath from games.json in ASI/shim dev-deploy
- Add automatic port retry to C++ UdpReceiver
- Take BuildOutputPath in dev-deploy and add loader/config auto-install
- Pass BuildOutputPath to dev-deploy helper
- Add toggle for world-space vs camera-local yaw
- Verify existing BepInEx loader arch and replace on mismatch
- Fall back to dev-tree vendor path in BepInEx install body

## [1.1.0] - 2026-05-01

### Added

- add Invoke-FetchLatestLoader and Refresh-VendoredLoader helpers

### Fixed

- install.cmd works on Program Files (x86) paths

### Other

- Adopt unified installer template, vendor BepInExPack, add chord bindings
- Add prediction-error correction to interpolators for smooth high-FPS output
- Port linear interpolation and quaternion SLERP smoothing from C# core
- Add gui_marker_compensation.h for RE Engine GUI world-anchor tracking
- Add REFramework utilities module (cameraunlock_reframework)
- Add velocity extrapolation to interpolators for smooth high-refresh output
- Gate UnityEngine.InputLegacyModule reference on file existence
- Fix batch paren-poisoning in install.cmd template
- Move game detection to data-driven games.json
- Fix install.cmd/uninstall.cmd templates for dev-tree use
- Unify installer CLI across BepInEx/MelonLoader/Cecil/ASI/REFramework/shim
- Make vendored loaders the install-time source of truth
- Add Step-SemanticVersion and Resolve-ReleaseVersion helpers
- Add camera discovery module (RTTI vtable + float classifier)
- Add AGENTS.md with shared code-quality and library API rules
- Expand submodule pointer commits in generated changelogs
- Fix /y flag detection and bundle vendored BepInEx in installers
- Use WriteAllBytes for .cmd output to avoid Defender race

## [1.0.2] - 2026-04-11

### Other

- Use absolute URL for README gif

## [1.0.1] - 2026-04-06

### Other

- Add Thunderstore icon and packaging
- Better Thunderstore display name
- Add LICENSE/CHANGELOG to Thunderstore ZIP, bump manifest version on release
- Widen changelog artifact paths to catch all release-relevant commits

## [1.0.0] - 2026-04-06

First release.
