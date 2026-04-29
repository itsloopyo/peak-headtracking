# bepinex (vendored)

This directory contains a bundled copy of the upstream mod loader. It is the install-time
source of truth: install.cmd extracts directly from here and never reaches out to the network.
Refresh manually with `pixi run update-deps`, then commit.

## Snapshot

- Asset: `5.4.75301`
- Upstream URL: https://thunderstore.io/package/download/BepInEx/BepInExPack_PEAK/5.4.75301/
- SHA-256: `a71432613ab0b2f560046bc341d949c9d853b4f5d5223ab5e7f2ef1f113522e7`
- Fetched at: 2026-04-28T17:57:08.7375957+01:00
- Source: direct-url

Do not edit this directory by hand. Run ``pixi run package`` (or CI release) to refresh.
