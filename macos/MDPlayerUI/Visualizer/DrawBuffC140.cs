// Port of the subset of MDPlayer/MDPlayerx64/drawBuff.cs's sprite-blit functions that
// frmC140.cs's screenDrawParams path calls. C140 (Namco System 2/21/NA-1 24-bit PCM sample
// player) is a 24-channel sample-playback chip - one row per channel (channel c drawn at
// y = (c+1)*8), each row showing a keyboard/note readout, independent L/R volume LED bars, a
// byte-packed L/R pan icon, two "bit" on/off flag icons (loop/noise-related control bits),
// and hex readouts of the sample frequency, ROM bank, start/end/loop addresses.
//
// Data source: reads chipRegister.pcmRegisterC140[chipId]/pcmKeyOnC140[chipId] directly -
// both are already public fields on ChipRegister (mirroring Audio.GetC140Register/
// GetC140KeyOn, which just forward to the same fields), so no new getter was needed for this
// chip, unlike most others in this port.
//
// Preserved quirk: pcmKeyOnC140[ch] is a live reference into the register-write side (set
// true by ChipRegister.writeC140 on key-on), and C140Visualizer.ScreenChangeParams clears it
// back to false after consuming it each frame - exactly as frmC140.cs's screenChangeParams
// does via Audio.GetC140KeyOn's direct array reference. Not copied into a local snapshot.
//
// Same deliberate simplification as every other DrawBuffXxx.cs in this port: `tp` is
// dropped/hardcoded to 0.
using MDPlayer;

namespace MDPlayer.UI.Visualizer
{
    public static class DrawBuffC140
    {
        public static SpriteAtlas RKbd = null!;
        public static SpriteAtlas RFont1_0 = null!; // rFont_01 (drawFont8, unmasked, t=0)
        public static SpriteAtlas RFont1_1 = null!; // rFont_02 (drawFont8, masked, t=1)
        public static SpriteAtlas RFont2_0 = null!; // rFont_03 (drawFont4/drawFont4Hex*, t=0)
        public static SpriteAtlas RType_0 = null!; // rType_01 (channel badge, unmasked)
        public static SpriteAtlas RType_1 = null!; // rType_02 (channel badge, masked)
        public static SpriteAtlas RVol = null!;
        public static SpriteAtlas RPan2 = null!; // rPan2_01 (2-tile byte-packed L/R pan icon)
        public static SpriteAtlas RNesDmc = null!; // rNESDMC (on/off switch icon)

        public static void LoadSprites()
        {
            RKbd = SpriteAtlas.Load("rKBD_01");
            RFont1_0 = SpriteAtlas.Load("rFont_01");
            RFont1_1 = SpriteAtlas.Load("rFont_02");
            RFont2_0 = SpriteAtlas.Load("rFont_03");
            RType_0 = SpriteAtlas.Load("rType_01");
            RType_1 = SpriteAtlas.Load("rType_02");
            RVol = SpriteAtlas.Load("rVol_01");
            RPan2 = SpriteAtlas.Load("rPan2_01");
            RNesDmc = SpriteAtlas.Load("rNESDMC");
        }

        // drawBuff.cs:3632 VolumeP.
        private static void VolumeP(PixelScreen screen, int x, int y, int t)
        {
            screen.DrawIntArray(x, y, RVol.Pixels, 32, 2 * t, 0, 2, 8 - (t / 4) * 4);
        }

        // drawBuff.cs:1040 VolumeToC140 - row-index overload, x fixed at 356.
        public static void VolumeToC140(PixelScreen screen, int row, int c, ref int ov, int nv)
        {
            if (ov == nv) return;

            int t = 0;
            int sy = 0;
            if (c == 1 || c == 2) { t = 4; }
            if (c == 2) { sy = 4; }
            int y = (row + 1) * 8;

            for (int i = 0; i <= 19; i++)
            {
                VolumeP(screen, 356 + i * 2, y + sy, 1 + t);
            }

            for (int i = 0; i <= nv; i++)
            {
                VolumeP(screen, 356 + i * 2, y + sy, i > 17 ? 2 + t : 0 + t);
            }

            ov = nv;
        }

        // drawBuff.cs:3638 drawKbn.
        public static void DrawKbn(PixelScreen screen, int x, int y, int t)
        {
            switch (t)
            {
                case 0: screen.DrawIntArray(x, y, RKbd.Pixels, 32, 0, 0, 4, 8); break;
                case 1: screen.DrawIntArray(x, y, RKbd.Pixels, 32, 4, 0, 3, 8); break;
                case 2: screen.DrawIntArray(x, y, RKbd.Pixels, 32, 8, 0, 4, 8); break;
                case 3: screen.DrawIntArray(x, y, RKbd.Pixels, 32, 12, 0, 4, 8); break;
                case 4: screen.DrawIntArray(x, y, RKbd.Pixels, 32, 0 + 16, 0, 4, 8); break;
                case 5: screen.DrawIntArray(x, y, RKbd.Pixels, 32, 4 + 16, 0, 3, 8); break;
                case 6: screen.DrawIntArray(x, y, RKbd.Pixels, 32, 8 + 16, 0, 4, 8); break;
                case 7: screen.DrawIntArray(x, y, RKbd.Pixels, 32, 12 + 16, 0, 4, 8); break;
            }
        }

        // drawBuff.cs:3680 drawFont8. t: 0=unmasked colour, 1=masked colour.
        public static void DrawFont8(PixelScreen screen, int x, int y, int t, string msg)
        {
            SpriteAtlas src = t == 0 ? RFont1_0 : RFont1_1;
            foreach (char c in msg)
            {
                int cd = c - 'A' + 0x20 + 1;
                screen.DrawIntArray(x, y, src.Pixels, 128, (cd % 16) * 8, (cd / 16) * 8, 8, 8);
                x += 8;
            }
        }

        // drawBuff.cs:3789 drawFont4.
        private static void DrawFont4(PixelScreen screen, int x, int y, int t, string msg)
        {
            foreach (char c in msg)
            {
                int cd = c - 'A' + 0x20 + 1;
                screen.DrawIntArray(x, y, RFont2_0.Pixels, 128, (cd % 32) * 4, (cd / 32) * 8, 4, 8);
                x += 4;
            }
        }

        // drawBuff.cs:4016 drawFont4HexByte / :3561 font4HexByte.
        private static void DrawFont4HexByte(PixelScreen screen, int x, int y, int num)
        {
            num = Common.Range(num, 0, 0xff);

            int n = num / 0x10;
            num -= n * 0x10;
            n = n > 0xf ? 0 : n;
            DrawFont4(screen, x, y, 0, Tables.hexCh[n]);

            n = num / 1;
            x += 4;
            DrawFont4(screen, x, y, 0, Tables.hexCh[n]);
        }

        public static void Font4HexByte(PixelScreen screen, int x, int y, ref int on, int nn)
        {
            if (on == nn) return;
            DrawFont4HexByte(screen, x, y, nn);
            on = nn;
        }

        // drawBuff.cs:4060 drawFont4Hex16Bit / :3577 font4Hex16Bit.
        private static void DrawFont4Hex16Bit(PixelScreen screen, int x, int y, int num)
        {
            num = Common.Range(num, 0, 0xffff);

            int n = num / 0x1000;
            num -= n * 0x1000;
            n = n > 0xf ? 0 : n;
            DrawFont4(screen, x, y, 0, Tables.hexCh[n]);

            n = num / 0x100;
            num -= n * 0x100;
            n = n > 0xf ? 0 : n;
            x += 4;
            DrawFont4(screen, x, y, 0, Tables.hexCh[n]);

            n = num / 0x10;
            num -= n * 0x10;
            n = n > 0xf ? 0 : n;
            x += 4;
            DrawFont4(screen, x, y, 0, Tables.hexCh[n]);

            n = num / 1;
            x += 4;
            DrawFont4(screen, x, y, 0, Tables.hexCh[n]);
        }

        public static void Font4Hex16Bit(PixelScreen screen, int x, int y, ref int on, int nn)
        {
            if (on == nn) return;
            DrawFont4Hex16Bit(screen, x, y, nn);
            on = nn;
        }

        // drawBuff.cs:1603 KeyBoardToC140 - row-index overload. Note: unlike the generic
        // KeyBoard() used by most other chips, this doesn't bound-check nt/ot against 12*8,
        // and the "clear" branch (nt < 0) prints blank spaces at the note-name position
        // rather than skipping - preserved exactly.
        public static void KeyBoardToC140(PixelScreen screen, int row, ref int ot, int nt)
        {
            if (ot == nt) return;

            int y = (row + 1) * 8;

            if (ot >= 0)
            {
                int kx = Tables.kbl[(ot % 12) * 2] + ot / 12 * 28;
                int kt = Tables.kbl[(ot % 12) * 2 + 1];
                DrawKbn(screen, 32 + kx, y, kt);
            }

            if (nt >= 0)
            {
                int kx = Tables.kbl[(nt % 12) * 2] + nt / 12 * 28;
                int kt = Tables.kbl[(nt % 12) * 2 + 1] + 4;
                DrawKbn(screen, 32 + kx, y, kt);
                DrawFont8(screen, 396, y, 1, Tables.kbn[nt % 12]);
                if (nt / 12 < 8)
                {
                    DrawFont8(screen, 412, y, 1, Tables.kbo[nt / 12]);
                }
            }
            else
            {
                DrawFont8(screen, 396, y, 1, "   ");
            }

            ot = nt;
        }

        // drawBuff.cs:4275 drawPanType2P / :1868 PanType2(screen,c,...) - the row-index
        // overload (fixed x=24). Same original function as the one already ported in
        // DrawBuffHuc6280.cs - duplicated here rather than cross-referenced, per this port's
        // file-independence convention.
        private static void DrawPanType2P(PixelScreen screen, int x, int y, int t)
        {
            int p = t & 0x0f;
            p = p == 0 ? 0 : 1 + p / 4;
            screen.DrawIntArray(x, y, RPan2.Pixels, 32, p * 4, 0, 4, 8);
            p = (t & 0xf0) >> 4;
            p = p == 0 ? 0 : 1 + p / 4;
            screen.DrawIntArray(x + 4, y, RPan2.Pixels, 32, p * 4, 0, 4, 8);
        }

        public static void PanType2(PixelScreen screen, int c, ref int ot, int nt)
        {
            if (ot == nt) return;
            DrawPanType2P(screen, 24, 8 + c * 8, nt);
            ot = nt;
        }

        // drawBuff.cs:3520 drawNESSw - generic 4x8 on/off icon.
        public static void DrawNesSw(PixelScreen screen, int x, int y, ref bool os, bool ns)
        {
            if (os == ns) return;
            screen.DrawIntArray(x, y, RNesDmc.Pixels, 64, (ns ? 1 : 0) * 4 + 32, 0, 4, 8);
            os = ns;
        }

        // drawBuff.cs:4382 ChC140_P - badge offset (16,0,16,8), distinct from AY8910/HuC6280's
        // (32,0,16,8) - and uses the 2-digit "d2" font4 form (up to 24 channels) rather than
        // drawFont8.
        private static void ChC140P(PixelScreen screen, int x, int y, int ch, bool mask)
        {
            SpriteAtlas src = mask ? RType_1 : RType_0;
            screen.DrawIntArray(x, y, src.Pixels, 128, 16, 0, 16, 8);
            DrawFont4(screen, x + 16, y, mask ? 1 : 0, (1 + ch).ToString("d2"));
        }

        // drawBuff.cs:2018 ChC140 - dirty-diff wrapper.
        public static void ChC140(PixelScreen screen, int ch, ref bool? om, bool? nm)
        {
            if (om == nm) return;
            ChC140P(screen, 0, 8 + ch * 8, ch, nm ?? false);
            om = nm;
        }
    }
}
