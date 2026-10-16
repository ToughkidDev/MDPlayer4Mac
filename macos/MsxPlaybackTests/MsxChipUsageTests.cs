using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using MDPlayer;
using MDPlayer.Driver.MUCOM;

internal static class MsxChipUsageTests
{
    internal static void Run(Action<bool, string> check, string fixtures)
    {
        byte[] basis = File.ReadAllBytes(Path.Combine(fixtures, "native-raw2-psg.mub"));
        byte[] Control(byte[] page, int part = 0)
            => ReplacePage(basis, part, page);

        foreach (byte[] page in new[] {
            new byte[] { 0 },
            new byte[] { 0xfa, 0x26, 225, 0 },
            new byte[] { 0xfa, 0x26, 0, 0xfa, 0x26, 255, 0 },
            new byte[] { 0xff, 0xf7, 0, 0, 0x26, 225, 0 },
            new byte[] { 0xff, 0xf7, 0, 0, 0x26, 0, 0xff, 0xf7, 0, 0, 0x26, 255, 0 },
            new byte[] { 0x98, 0xfa, 0x26, 198, 0x84, 0 },
        })
        {
            byte[] packet = Control(page);
            check(!MsxMubProfile.Parse(packet).UsesOpna,
                "Sequencer-only OPNA page registered an audio chip: " + Convert.ToHexString(page));
            var session = MusicEngine.Load(packet, "control-only.mub");
            check(session != null, "Native PSG session failed without audible OPNA");
            check(session != null && !HasOpna(session), "Mixer registered sequencing-only OPNA");
        }

        // Conservatively retain real sound, indirect writes (including CSM), unknown
        // commands and malformed instructions. Never scan operand bytes as opcodes.
        foreach (byte[] page in new[] {
            new byte[] { 0x18, 0x40, 0 }, // note
            new byte[] { 0xfa, 0x28, 0xf0, 0 }, // FM key-on
            new byte[] { 0xfa, 0x08, 15, 0 }, // SSG level
            new byte[] { 0xfa, 0x10, 1, 0 }, // rhythm trigger
            new byte[] { 0xff, 0xf7, 0, 1, 0, 0xa0, 0 }, // ADPCM control
            new byte[] { 0xfa, 0x27, 0x80, 0 }, // CSM
            new byte[] { 0xf0, 0xfa, 0 }, // voice operand resembles raw write
            new byte[] { 0xff, 0xf7, 0, 1, 0x26, 225, 0 }, // different port: not Timer B
            new byte[] { 0xff, 0xf7, 1, 0, 0x26, 225, 0 }, // different chip: retain conservatively
            new byte[] { 0xff, 0xf7, 0, 0, 0x27, 0x80, 0 }, // indirect CSM
            new byte[] { 0xfa, 0x26, 225, 0xf0, 0, 0 },
            new byte[] { 0xfa, 0x26 }, // truncated timer instruction
            new byte[] { 0xff, 0xf7, 0, 0, 0x26 }, // truncated extended timer instruction
            new byte[] { 0xfa, 0x26, 225 }, // missing terminator
            new byte[] { 0, 0xfa, 0x28, 0xf0, 0 }, // unclassified trailing bytes
        })
        {
            byte[] packet = Control(page);
            check(MsxMubProfile.Parse(packet).UsesOpna,
                "Potential audible/unknown OPNA page discarded: " + Convert.ToHexString(page));
        }
        // Loop targets are execution entry points, including after an otherwise
        // silent first pass. An operand can become a note if the header jumps there.
        byte[] loopControl = { 0xfa, 0x26, 0x40, 0x98, 0 };
        foreach (int loop in new[] { -1, 0, 1, 2, 3, 4 })
        {
            var profile = MsxMubProfile.Parse(ReplacePage(basis, 0, loopControl, loop));
            check(profile.UsesOpna == (loop is 1 or 2), "Direct timer loop target misclassified: " + loop);
        }
        byte[] extendedLoop = { 0xff, 0xf7, 0, 0, 0x26, 0x40, 0x98, 0 };
        for (int loop = -1; loop < extendedLoop.Length; loop++)
            check(MsxMubProfile.Parse(ReplacePage(basis, 0, extendedLoop, loop)).UsesOpna
                == (loop is >= 1 and <= 5), "Extended timer loop target misclassified: " + loop);
        check(LoopEmitsKeyOn(ReplacePage(basis, 0, loopControl, 1)),
            "Operand-target loop did not exercise an actual OPNA key-on in the sequencer");

        // Cover the complete operand range: zero is data, never an early end.
        for (int value = 0; value < 256; value++)
        {
            check(!MsxMubProfile.Parse(Control(new byte[] { 0xfa, 0x26, (byte)value, 0 })).UsesOpna,
                "Direct Timer-B operand misclassified: " + value);
            check(!MsxMubProfile.Parse(Control(new byte[] { 0xff, 0xf7, 0, 0, 0x26, (byte)value, 0 })).UsesOpna,
                "Extended Timer-B operand misclassified: " + value);
        }
        foreach (byte opcode in new byte[] { 0x40, 0x98, 0xfa, 0xff })
            check(MsxMubProfile.Parse(Control(new[] { opcode })).UsesOpna,
                "One-byte unterminated OPNA page discarded: " + opcode);
        byte[] maximumPage = Enumerable.Repeat((byte)0x81, 60_000).ToArray();
        maximumPage[^1] = 0;
        check(!MsxMubProfile.Parse(Control(maximumPage)).UsesOpna, "Maximum-size rest page failed");

        // No native instrument remains: retain a silent rendering clock rather
        // than turning a previously accepted control/rest-only score into a load error.
        byte[] silent = Control(new byte[] { 0xfa, 0x26, 225, 0x98, 0 });
        for (int part = 11; part < 21; part++) silent = ReplacePage(silent, part, new byte[] { 0 });
        var silentSession = MusicEngine.Load(silent, "silent-control.mub");
        check(silentSession != null, "Silent control/rest-only score rejected");
        if (silentSession != null)
        {
            var silentAudio = Render(silent, "silent-control.mub");
            check(silentAudio.Stopped && silentAudio.Peak == 0, "Silent score failed to end silently");
        }

        // Check every OPNA part and every page, rather than special-casing A0.
        for (int part = 0; part < 11; part++)
        {
            byte[] packet = ReplacePage(Control(new byte[] { 0 }), part,
                new byte[] { 0xff, 0xf7, 0, 0, 0x26, 225, 0 });
            check(!MsxMubProfile.Parse(packet).UsesOpna, "Timer-only part registered OPNA: " + part);
            packet = ReplacePage(packet, part, new byte[] { 0xfa, 0x28, 0xf0, 0 });
            check(MsxMubProfile.Parse(packet).UsesOpna, "Audible part discarded: " + part);
        }
        // FA has a different meaning in D/E/F. Envelope operands can resemble
        // a timer command followed by rests; they must never suppress OPNA.
        for (int part = 3; part <= 5; part++)
        {
            byte[] packet = ReplacePage(Control(new byte[] { 0 }), part,
                new byte[] { 0xfa, 0x26, 0xe1, 0x80, 0x90, 0xa0, 0xb0, 0 });
            check(MsxMubProfile.Parse(packet).UsesOpna, "SSG envelope mistaken for Timer B: " + part);
            packet = ReplacePage(packet, part, new byte[] { 0xfa, 0x26, 225, 0 });
            check(MsxMubProfile.Parse(packet).UsesOpna, "Truncated SSG envelope mistaken for Timer B: " + part);
        }
        foreach (bool audible in new[] { false, true })
        {
            string source = "#voice voice.dat\nA0 t225\nA1 "
                + (audible ? "@0 v12 o4 c8 r8" : "t225")
                + "\nMSXD @0 v15 o4 c8 r8\n";
            string path = Path.Combine(fixtures, "multipage.muc");
            byte[] packet = MucomDotNET.CompileMuc(System.Text.Encoding.ASCII.GetBytes(source), path, out string error)
                ?? throw new Exception(error);
            check(MsxMubProfile.Parse(packet).UsesOpna == audible,
                "Second OPNA page was not classified independently: " + audible);
        }

        // These real toolchain fixtures put the authored tempo in the native lane,
        // so removing the redundant A timer page changes no music or timing. The
        // MUC adapter still inserts its A t225: exercise the actual common loader.
        foreach (string name in new[] { "native-raw2-psg", "native-raw2-scc", "native-raw2-scc-plus" })
        {
            string mucPath = Path.Combine(fixtures, name + ".muc");
            byte[] muc = File.ReadAllBytes(mucPath);
            byte[] compiled = MucomDotNET.CompileMuc(muc, mucPath, out string error)
                ?? throw new Exception(error);
            byte[] packed = ReplacePage(compiled, 0, new byte[] { 0 });
            byte[] toolchain = File.ReadAllBytes(Path.Combine(fixtures, name + ".mub"));
            var fromSource = Render(muc, mucPath);
            var fromCompiled = Render(compiled, Path.ChangeExtension(mucPath, ".mub"));
            var withoutControl = Render(packed, Path.ChangeExtension(mucPath, ".mub"));
            var fromToolchain = Render(toolchain, Path.ChangeExtension(mucPath, ".mub"));
            check(fromSource.Chips == withoutControl.Chips, name + ": MUC/MUB audio chip set differs");
            check(fromSource == fromCompiled, name + ": source/compiled loader differs");
            check(fromSource == withoutControl, name + ": redundant tempo page changes PCM/timing/end state");
            check(fromSource == fromToolchain, name + ": MUC/toolchain MUB PCM/timing differs");
            check(fromSource.Peak > 0 && fromSource.Stopped, name + ": missing finite non-silent playback");
            Console.WriteLine($"CHIP PARITY {name} source={fromSource} no-control={withoutControl}");
        }

        string loopPath = Path.Combine(fixtures, "native-raw2-psg.muc");
        string loopSource = File.ReadAllText(loopPath).Replace("r%24", "L r%24");
        byte[] loopMuc = System.Text.Encoding.ASCII.GetBytes(loopSource);
        byte[] loopMub = MucomDotNET.CompileMuc(loopMuc, loopPath, out string loopError)
            ?? throw new Exception(loopError);
        var loopAudio = Render(loopMuc, loopPath);
        var loopWithoutControl = Render(ReplacePage(loopMub, 0, new byte[] { 0 }), Path.ChangeExtension(loopPath, ".mub"));
        check(loopAudio.Loops >= 2 && !loopAudio.Stopped, "Authored loop regression did not reach two loop boundaries");
        check(loopAudio == loopWithoutControl, "Redundant timer page changes loop timing/PCM");
        Console.WriteLine($"CHIP LOOP source={loopAudio} no-control={loopWithoutControl}");

        // Genuine OPNA+native music and SCC-SSG noise/envelope fallback still need OPNA.
        foreach (string source in new[] {
            "#voice voice.dat\nA @0 v12 o4 c8 r8\nMSXD @0 v15 o4 c8 r8\n",
            "#voice voice.dat\nG @1 c8 r8\nMSXD @0 v15 c8 r8\n",
            "#voice voice.dat\nK y0,0,40,240 r%16\nMSXD @0 v15 c8 r8\n",
            "#voice voice.dat\n#SSGExtend On\nD @0 @I8 v15 o4 c8 r8\n",
        })
        {
            string path = Path.Combine(fixtures, "chip-regression.muc");
            byte[] compiled = MucomDotNET.CompileMuc(System.Text.Encoding.ASCII.GetBytes(source), path, out string error)
                ?? throw new Exception(error);
            var session = MusicEngine.Load(compiled, Path.ChangeExtension(path, ".mub"));
            check(session != null && HasOpna(session), "Required OPNA omitted from mixed/SCC-SSG mixer");
        }

        // Alternating chip sets must not carry normalization or device state from
        // a prior session. Compare complete finite captures in both load orders.
        string[] reloadNames = { "native-raw2-psg", "legacy-psg", "native-raw2-scc", "native-raw2-scc-plus" };
        var reloadAudio = reloadNames.ToDictionary(name => name, name =>
        {
            string path = Path.Combine(fixtures, name + ".mub");
            return Render(File.ReadAllBytes(path), path);
        });
        for (int pass = 0; pass < 3; pass++)
        foreach (string name in pass % 2 == 0 ? reloadNames.Reverse() : reloadNames)
        {
            string path = Path.Combine(fixtures, name + ".mub");
            check(Render(File.ReadAllBytes(path), path) == reloadAudio[name],
                "Chip-set reload changed PCM/timing: " + name + " pass " + pass);
        }

        // Legacy headers retain their existing chip detection, even for timer-only MUC.
        foreach (string name in new[] { "legacy-psg", "legacy-extended", "pcm-invert" })
        {
            string path = Path.Combine(fixtures, name + ".mub");
            var session = MusicEngine.Load(File.ReadAllBytes(path), path);
            check(session != null && HasOpna(session), "Legacy OPNA detection changed: " + name);
        }
    }

    internal static void DumpFixtures(string fixtures, string output)
    {
        var signatures = new Dictionary<string, object>();
        foreach (string path in Directory.GetFiles(fixtures, "*.mub").OrderBy(x => x))
            signatures[Path.GetFileName(path)] = Render(File.ReadAllBytes(path), path);
        File.WriteAllText(output, System.Text.Json.JsonSerializer.Serialize(signatures,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }

    private static bool LoopEmitsKeyOn(byte[] packet)
    {
        var session = MusicEngine.Load(packet, "loop-entry.mub") ?? throw new Exception("Loop test load failed");
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var inner = session.Driver.GetType().GetField("driver", flags)!.GetValue(session.Driver)!;
        var work = (mucomDotNET.Driver.Work)inner.GetType().GetField("work", flags)!.GetValue(inner)!;
        var music = (mucomDotNET.Driver.Music2)inner.GetType().GetField("music2", flags)!.GetValue(inner)!;
        var callback = inner.GetType().GetField("WriteOPNAP", flags)!;
        var original = (Action<musicDriverInterface.ChipDatum>)callback.GetValue(inner)!;
        bool keyOn = false;
        callback.SetValue(inner, (Action<musicDriverInterface.ChipDatum>)(d => {
            if (d.port == 0 && d.address == 0x28 && (d.data & 0xf0) != 0) keyOn = true;
            original(d);
        }));
        for (int tick = 0; tick < 256 && work.Status > 0 && !keyOn; tick++) music.PL_SND();
        return keyOn;
    }

    private static bool HasOpna(MusicEngineSession session)
        => session.ChipClocks.ContainsKey(MDSound.MDSound.enmInstrumentType.YM2608);

    private sealed record Audio(string Chips, string PcmSha256, long Frames, uint Loops, bool Stopped, int Peak);

    private static Audio Render(byte[] data, string path)
    {
        var session = MusicEngine.Load(data, path) ?? throw new Exception("Load failed: " + path);
        using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        short[] samples = new short[4096];
        long frames = 0;
        int peak = 0;
        while (!session.Driver.Stopped && session.Driver.vgmCurLoop < 2 && frames < session.SampleRate * 12)
        {
            int count = session.RenderSamplesWithMasterVolume(samples, 0, samples.Length);
            if (count <= 0) throw new Exception("No PCM output: " + path);
            frames += count / 2;
            digest.AppendData(MemoryMarshal.AsBytes(samples.AsSpan(0, count)));
            foreach (short sample in samples.AsSpan(0, count)) peak = Math.Max(peak, Math.Abs((int)sample));
        }
        if (session.Driver is MucomDotNET driver && driver.PlaybackError.Length != 0)
            throw new Exception(driver.PlaybackError);
        return new Audio(session.DescribeActiveChips(), Convert.ToHexString(digest.GetHashAndReset()),
            frames, session.Driver.vgmCurLoop, session.Driver.Stopped, peak);
    }

    private static byte[] ReplacePage(byte[] packet, int targetPart, byte[] replacement, int loop = -1)
    {
        // Independent test packet builder: retain every descriptor, companion block,
        // authored page and tag; change just one page extent and its body.
        int at = 34;
        for (int chip = 0; chip < 5; chip++)
        {
            at += 23;
            int instruments = packet[at++]; at += 2 * instruments;
            int pcms = packet[at++]; at += 2 * pcms;
        }
        int countsAt = at; at += 55;
        int pageCount = BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(12));
        int definitionsAt = at;
        at += pageCount * 8;
        at += 4 * (BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(14))
            + BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(16)));
        int page = 0;
        for (int part = 0; part < 55; part++)
        for (int p = 0; p < packet[countsAt + part]; p++, page++)
        {
            int extent = definitionsAt + page * 8;
            int length = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(extent)));
            if (part == targetPart && p == 0)
            {
                int difference = replacement.Length - length;
                byte[] result = new byte[packet.Length + difference];
                packet.AsSpan(0, at).CopyTo(result);
                replacement.CopyTo(result, at);
                packet.AsSpan(at + length).CopyTo(result.AsSpan(at + replacement.Length));
                BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(extent), (uint)replacement.Length);
                BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(extent + 4), loop);
                uint tag = BinaryPrimitives.ReadUInt32LittleEndian(result.AsSpan(18));
                if (tag != 0) BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(18), checked((uint)(tag + difference)));
                return result;
            }
            at += length;
        }
        throw new Exception("Test page not found: " + targetPart);
    }
}
