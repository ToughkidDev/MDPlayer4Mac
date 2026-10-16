# MUCOM compiler overlay

`muc88.cs` derives from kuma4649/mucomDotNET f040376 (GPL-3.0); see
`../ThirdParty/mucomDotNET/LICENSE.txt`. All remaining sources/resources are linked
from that pinned submodule without modification.

Two conditions in SOU1/SOD1 now require `CHCHK() == ChannelType.ADPCM` for
`#PCMInvert`. Upstream applied it to every part, despite the documented PCM-only
meaning. Global `#invert` behavior remains unchanged. Tests compare music data
rather than tag bytes and verify both K inversion and D isolation.

Rebase this file together with the driver overlay when changing the submodule.

The 2026-10-13 Work comparison also fixes `pm1 p4..p6`: every selected
drum's expanded F8 command now carries its own validated wait operand.
Omitted waits use the current default length; a single trailing operand
previously made playback decode later pan commands as long notes.
Tests compare 768 combinations against independently expanded packed-pan
source and require equal MUC/toolchain-MUB duration for RhythmMute fixtures.
