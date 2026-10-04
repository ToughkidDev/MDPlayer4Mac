#!/bin/bash
#
# Decodes generated 440 Hz test tones in every supported audio-file format through
# EngineSmokeTest and checks the rendered WAV (length, pitch, level). On macOS this
# exercises the AudioToolbox decoder (afconvert creates the AIFF/M4A/AAC/FLAC/CAF
# files); OGG and MP3 are added when ffmpeg is available.
#
# Usage: macos/tools/audio-file-smoke.sh [work-dir]

set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
WORK="${1:-$(mktemp -d)}"
mkdir -p "$WORK"
cd "$WORK"

python3 - <<'EOF'
import math, struct, wave
rate, seconds = 48000, 2.0
with wave.open("tone.wav", "wb") as w:
    w.setnchannels(2)
    w.setsampwidth(2)
    w.setframerate(rate)
    frames = bytearray()
    for i in range(int(rate * seconds)):
        v = int(12000 * math.sin(2 * math.pi * 440 * i / rate))
        frames += struct.pack("<hh", v, v)
    w.writeframes(bytes(frames))
EOF

INPUTS=(tone.wav)
if command -v afconvert >/dev/null 2>&1; then
    afconvert -f AIFF -d BEI16 tone.wav tone.aiff && INPUTS+=(tone.aiff)
    afconvert -f m4af -d aac tone.wav tone.m4a && INPUTS+=(tone.m4a)
    afconvert -f adts -d aac tone.wav tone.aac && INPUTS+=(tone.aac)
    afconvert -f flac -d flac tone.wav tone.flac && INPUTS+=(tone.flac)
fi
if command -v ffmpeg >/dev/null 2>&1; then
    ffmpeg -loglevel error -y -i tone.wav -c:a libmp3lame tone.mp3 && INPUTS+=(tone.mp3) || true
    ffmpeg -loglevel error -y -i tone.wav -c:a libvorbis tone.ogg && INPUTS+=(tone.ogg) || true
fi

SMOKE=(dotnet run --project "$ROOT/macos/EngineSmokeTest/EngineSmokeTest.csproj" -c Release --no-build --)
FAILED=0
for input in "${INPUTS[@]}"; do
    out="out_${input}.wav"
    if ! "${SMOKE[@]}" "$WORK/$input" "$WORK/$out" | grep -E '^(Wrote|.* chips:)'; then
        echo "FAIL $input: EngineSmokeTest could not render it"
        FAILED=1
        continue
    fi
    if ! python3 - "$out" "$input" <<'EOF'
import struct, sys, wave
path, name = sys.argv[1], sys.argv[2]
w = wave.open(path)
n, rate, ch = w.getnframes(), w.getframerate(), w.getnchannels()
data = struct.unpack("<%dh" % (n * ch), w.readframes(n))
left = data[0::ch]
loud = [i for i, v in enumerate(left) if abs(v) > 500]
length = (loud[-1] - loud[0] + 1) / rate if loud else 0
mid = left[int(rate * 0.5):int(rate * 1.5)]
freq = sum(1 for a, b in zip(mid, mid[1:]) if a < 0 <= b)
peak = max(abs(v) for v in left)
ok = 1.9 <= length <= 2.15 and 430 <= freq <= 450 and peak > 3000
print("%s %s: length=%.3fs freq=%dHz peak=%d" % ("ok  " if ok else "FAIL", name, length, freq, peak))
sys.exit(0 if ok else 1)
EOF
    then
        FAILED=1
    fi
done
exit "$FAILED"
