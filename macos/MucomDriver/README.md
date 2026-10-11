# MUCOMSX playback overlay

This project builds as `mucomDotNETDriver`, replacing the original driver's project
reference in the four macOS consumers. It shares the compiler and common library
with the pinned `ThirdParty/mucomDotNET` submodule (f040376). No submodule edits are
required to build this feature.

`driver.cs` and `music2.cs` are derived from kuma4649/mucomDotNET f040376 and retain
its GPL-3.0 license. The original license is in
`../ThirdParty/mucomDotNET/LICENSE.txt`. Other driver sources are linked from that
submodule. If its revision changes, rebase these two overlay files before updating.

The overlay also retains two pre-existing local fixes: OPNA timer acknowledgments
preserve register 0x27's FM3 mode bits, and K-part `y` writes to FM registers use
port 1. The original dirty submodule was left untouched.

MSX hooks use the original page/repeat/tempo scheduler and only change the sound
sinks, SSG extension commands and native shared timer. `MsxExtensions.cs` holds
page-local waveform/gain state and handles disk `FF ED`/`FF EE` commands. In-memory
MUC compilation uses upstream SSG encodings (`FF F6`/`FF FB`) without moving bytes
or invalidating repeat offsets. These in-memory buffers are not MUB export files.

The core `MsxSoundMapper` ports MUCOMSX `MSXTargets.asm`, `MSXTargetCore.asm` and
`SSGExtendDriver.asm` (2026-10-11). Its YM2413 ROM table comes from Aaron Giles'
ymfm; the BSD-3-Clause notice is preserved in `YMFM-LICENSE.txt`.

See [MSX playback](../docs/MSX-PLAYBACK.md) for semantics and regression commands.
