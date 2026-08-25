// Pixel-art mixer view modelled after the Windows frmMixer2/drawBuff mixer.  It retains the
// original rFader sprite sheet, 4px font and dB curve, but puts the active-chip faders in
// horizontal rows so a long travel distance makes fine adjustment practical on a Mac.
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Input;
using MDPlayer;

namespace MDPlayer.UI.Visualizer
{
    public sealed class MixerVisualizer
    {
        private const int DefaultScreenWidth = 354;
        private const int MinimumScreenWidth = 160;
        private const int DisplayScale = 2;
        // Keep the Windows-style fader graphics intact, but pack consecutive faders at
        // half of the former row pitch so a multi-block chip stays easy to scan.
        private const int RowHeight = 14;
        private const int FaderX = 80;
        private const int FaderY = 3;
        private const int FaderKnobWidth = 13;

        private sealed class Slot
        {
            public ChipVolumeKey? ChipKey;
            public string Label = "";
        }

        private readonly MusicEngineSession session;
        private readonly List<Slot> slots = new();
        private readonly SpriteAtlas fader = SpriteAtlas.Load("rFader");
        private readonly SpriteAtlas font = SpriteAtlas.Load("rFont_03");
        private int draggingSlot = -1;

        public PixelScreen Screen { get; }
        public event Action<ChipVolumeKey, int>? ChipVolumeChanged;
        public event Action<int>? MasterVolumeChanged;

        public MixerVisualizer(MusicEngineSession session)
        {
            this.session = session;
            slots.Add(new Slot { Label = "MST" });
            if (session.ChipVolumeSlots.Count > 0)
            {
                slots.AddRange(session.ChipVolumeSlots
                    .OrderBy(slot => slot.Key.Type.ToString())
                    .ThenBy(slot => slot.Key.ChipId)
                    .Select(slot => new Slot
                    {
                        ChipKey = slot.Key,
                        Label = ChipLabel(slot.Key, session.ChipVolumeSlots, session.UsesK052539SccPlus),
                    }));
            }
            else
            {
                // Compatibility fallback for session types that do not expose MDSound chip
                // records (for example renderers that only provide a master fader).
                slots.AddRange(session.ChipVolumes.Keys
                    .OrderBy(type => type.ToString())
                    .Select(type => new Slot
                    {
                        ChipKey = new ChipVolumeKey(type, 0),
                        Label = ShortName(type, session.UsesK052539SccPlus),
                    }));
            }

            Screen = new PixelScreen();
            // Channel visualizers present their original pixel fonts at 2x.  Use that same
            // nearest-neighbour scale here so the 4px rFont_03 labels and every rFader
            // graphic are equally legible.  This provisional width is replaced with the
            // measured channel-view width just before the volume view is shown.
            Screen.Init(DefaultScreenWidth, Math.Max(1, slots.Count) * RowHeight, zoom: DisplayScale);
            Screen.PointerPressed += OnPointerPressed;
            Screen.PointerMoved += OnPointerMoved;
            Screen.PointerReleased += OnPointerReleased;
            Screen.PointerWheelChanged += OnPointerWheelChanged;
            Refresh();
        }

        // PixelScreen dimensions are native pixel dimensions while Bounds are Avalonia
        // display units.  Every channel visualizer uses the same 2x scale, so converting
        // its measured width back to native pixels makes this view exactly as wide.
        public void FitToChannelViewWidth(double channelViewDisplayWidth)
        {
            if (channelViewDisplayWidth <= 0) return;

            int nativeWidth = Math.Max(MinimumScreenWidth,
                (int)Math.Round(channelViewDisplayWidth / DisplayScale));
            if (nativeWidth == Screen.NativeWidth) return;

            Screen.Init(nativeWidth, Math.Max(1, slots.Count) * RowHeight, zoom: DisplayScale);
            Refresh();
        }

        private int FaderLength => Math.Max(8, ((Screen.NativeWidth - FaderX - 8) / 8) * 8);

        public void Refresh()
        {
            Array.Fill(Screen.Buffer, unchecked((int)0xff000000));
            for (int index = 0; index < slots.Count; index++)
            {
                DrawSlot(index, slots[index]);
            }
            Screen.Present();
        }

        private void DrawSlot(int index, Slot slot)
        {
            int baseY = index * RowHeight;
            int volume = slot.ChipKey is { } key ? session.GetChipVolume(key) : session.MasterVolume;

            DrawText(4, baseY + 2, slot.Label);
            string db = volume > 0 ? $"+{volume}" : volume.ToString();
            DrawText(36, baseY + 2, db);

            int knobPosition = FaderPosition(volume);
            DrawFaderSlit(FaderX, baseY + FaderY);
            // Draw the gauge inside the rail and terminate it at the knob's centre.  Both
            // elements now use the exact same non-linear dB position, not two independent
            // volume-to-pixel conversions.
            DrawMeter(FaderX, baseY + FaderY + 3, knobPosition + FaderKnobWidth / 2);

            // The original master/chip knob sprites are 8x13 vertical images.  Rotating
            // them at blit time makes a crisp 13x8 horizontal knob without a second asset.
            DrawRotatedClockwise(FaderX + knobPosition, baseY + FaderY,
                slot.ChipKey == null ? 0 : 8, 0, 8, 13);
        }

        private void DrawFaderSlit(int x, int y)
        {
            // rFader's caps and rail tiles are vertical in the Windows bitmap.  Reverse the
            // cap order while rotating so the conventional horizontal direction remains
            // quiet on the left and loud on the right.
            DrawRotatedClockwise(x, y, 24, 0, 8, 8);
            for (int offset = 8; offset < FaderLength - 8; offset += 8)
            {
                DrawRotatedClockwise(x + offset, y, 16, 8, 8, 8);
            }
            DrawRotatedClockwise(x + FaderLength - 8, y, 16, 0, 8, 8);
        }

        private void DrawMeter(int x, int y, int knobCentre)
        {
            // frmMixer2's slim 2px rFader level bar, overlaid on the rail and anchored to
            // the moving knob rather than being a separately positioned indicator.
            int level = Math.Clamp(knobCentre, 0, FaderLength);
            for (int i = 0; i < FaderLength; i += 2)
            {
                int tile = i < level ? (i >= level - 8 ? 0 : 1) : 2;
                Screen.DrawIntArray(x + i, y, fader.Pixels, fader.Width, 24, 8 + tile, 2, 1);
            }
        }

        private int FaderPosition(int volume)
        {
            int usableLength = FaderLength - FaderKnobWidth;
            // frmMixer2 splits the 43px vertical travel into 35px for attenuation and 8px
            // for positive gain.  Preserve that proportion over this longer horizontal bar.
            int zeroDbPosition = usableLength * 35 / 43;
            return volume <= 0
                ? (int)Math.Round((volume + 192) * zeroDbPosition / 192.0)
                : zeroDbPosition + (int)Math.Round(volume * (usableLength - zeroDbPosition) / 20.0);
        }

        private int VolumeAtPosition(double position)
        {
            int usableLength = FaderLength - FaderKnobWidth;
            int zeroDbPosition = usableLength * 35 / 43;
            double clamped = Math.Clamp(position, 0, usableLength);
            return clamped <= zeroDbPosition
                ? (int)Math.Round(-192 + clamped * 192.0 / zeroDbPosition)
                : (int)Math.Round((clamped - zeroDbPosition) * 20.0 / (usableLength - zeroDbPosition));
        }

        private void DrawRotatedClockwise(int x, int y, int sourceX, int sourceY, int width, int height)
        {
            for (int sourceRow = 0; sourceRow < height; sourceRow++)
            {
                for (int sourceColumn = 0; sourceColumn < width; sourceColumn++)
                {
                    int targetX = x + height - 1 - sourceRow;
                    int targetY = y + sourceColumn;
                    if ((uint)targetX >= Screen.NativeWidth || (uint)targetY >= Screen.NativeHeight) continue;
                    Screen.Buffer[targetY * Screen.NativeWidth + targetX]
                        = fader.Pixels[(sourceY + sourceRow) * fader.Width + sourceX + sourceColumn];
                }
            }
        }

        private void DrawText(int x, int y, string text)
        {
            foreach (char character in text)
            {
                int code = character - 'A' + 0x20 + 1;
                Screen.DrawIntArray(x, y, font.Pixels, font.Width, (code % 32) * 4, (code / 32) * 8, 4, 8);
                x += 4;
            }
        }

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            int slot = SlotAt(e.GetPosition(Screen).X, e.GetPosition(Screen).Y);
            if (slot < 0) return;

            draggingSlot = slot;
            e.Pointer.Capture(Screen);
            SetVolumeFromPointer(slot, e.GetPosition(Screen).X);
            e.Handled = true;
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (draggingSlot < 0 || e.Pointer.Captured != Screen) return;
            SetVolumeFromPointer(draggingSlot, e.GetPosition(Screen).X);
        }

        private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            draggingSlot = -1;
            if (e.Pointer.Captured == Screen) e.Pointer.Capture(null);
        }

        private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            int slot = SlotAt(e.GetPosition(Screen).X, e.GetPosition(Screen).Y);
            if (slot < 0 || e.Delta.Y == 0) return;

            SetSlotVolume(slot, CurrentVolume(slot) + Math.Sign(e.Delta.Y));
            e.Handled = true;
        }

        private int SlotAt(double displayX, double displayY)
        {
            if (Screen.Bounds.Width <= 0 || Screen.Bounds.Height <= 0) return -1;
            int x = (int)(displayX * Screen.NativeWidth / Screen.Bounds.Width);
            int y = (int)(displayY * Screen.NativeHeight / Screen.Bounds.Height);
            if (x < FaderX || x >= FaderX + FaderLength) return -1;
            int slot = y / RowHeight;
            return slot >= 0 && slot < slots.Count ? slot : -1;
        }

        private void SetVolumeFromPointer(int slot, double displayX)
        {
            double nativeX = displayX * Screen.NativeWidth / Math.Max(1, Screen.Bounds.Width);
            // Map the pointer to the centre of the 13px-wide knob, so clicking an existing
            // knob does not nudge its value by a few dB before the user starts dragging.
            SetSlotVolume(slot, VolumeAtPosition(nativeX - FaderX - FaderKnobWidth / 2.0));
        }

        private int CurrentVolume(int slot)
        {
            Slot item = slots[slot];
            return item.ChipKey is { } key ? session.GetChipVolume(key) : session.MasterVolume;
        }

        private void SetSlotVolume(int slot, int volume)
        {
            Slot item = slots[slot];
            int clamped = Math.Clamp(volume, -192, 20);
            if (CurrentVolume(slot) == clamped) return;

            if (item.ChipKey is { } key)
            {
                session.SetChipVolume(key, clamped);
                ChipVolumeChanged?.Invoke(key, clamped);
            }
            else
            {
                session.SetMasterVolume(clamped);
                MasterVolumeChanged?.Invoke(clamped);
            }
            Refresh();
        }

        private static string ShortName(MDSound.MDSound.enmInstrumentType type, bool usesK052539SccPlus = false) => type switch
        {
            MDSound.MDSound.enmInstrumentType.SN76489 => "DCSG",
            MDSound.MDSound.enmInstrumentType.YM2612 => "OPN2",
            MDSound.MDSound.enmInstrumentType.YM2151 => "OPM",
            MDSound.MDSound.enmInstrumentType.YM2151x68soundPCM => "ADPCM",
            MDSound.MDSound.enmInstrumentType.YM2203 => "OPN",
            MDSound.MDSound.enmInstrumentType.YM2608 => "OPNA",
            MDSound.MDSound.enmInstrumentType.YM2609 => "OPN9",
            MDSound.MDSound.enmInstrumentType.YM2610 => "OPNB",
            MDSound.MDSound.enmInstrumentType.YM2413 => "OPLL",
            MDSound.MDSound.enmInstrumentType.YM3526 => "OPL",
            MDSound.MDSound.enmInstrumentType.YM3812 => "OPL2",
            MDSound.MDSound.enmInstrumentType.YMF262 => "OPL3",
            MDSound.MDSound.enmInstrumentType.YMF278B => "OPL4",
            MDSound.MDSound.enmInstrumentType.YMF271 => "OPX",
            MDSound.MDSound.enmInstrumentType.Y8950 => "Y895",
            MDSound.MDSound.enmInstrumentType.YMZ280B => "YMZ",
            MDSound.MDSound.enmInstrumentType.RF5C164 => "RF16",
            MDSound.MDSound.enmInstrumentType.RF5C68 => "RF68",
            MDSound.MDSound.enmInstrumentType.SEGAPCM => "SPCM",
            MDSound.MDSound.enmInstrumentType.OKIM6258 => "OKI6",
            MDSound.MDSound.enmInstrumentType.OKIM6295 => "OKI9",
            MDSound.MDSound.enmInstrumentType.MultiPCM => "MPCM",
            MDSound.MDSound.enmInstrumentType.K051649 => usesK052539SccPlus ? "SCC+" : "SCC",
            MDSound.MDSound.enmInstrumentType.K053260 => "K053",
            MDSound.MDSound.enmInstrumentType.K054539 => "K054",
            MDSound.MDSound.enmInstrumentType.Nes => "NES",
            _ => type.ToString()[..Math.Min(4, type.ToString().Length)],
        };

        private static string ChipLabel(
            ChipVolumeKey key,
            IReadOnlyCollection<ChipVolumeSlot> allSlots,
            bool usesK052539SccPlus)
        {
            string label = ShortName(key.Type, usesK052539SccPlus);
            bool isDualChip = allSlots
                .Where(slot => slot.Key.Type == key.Type)
                .Select(slot => slot.Key.ChipId)
                .Distinct()
                .Skip(1)
                .Any();
            if (isDualChip) label += (key.ChipId + 1).ToString();

            // The original 4px font has a 32px label area before the dB readout. Keep the
            // source names compact while making the separate blocks unmistakable:
            // OPNAFM/OPNASSG/OPNARHY/OPNAADP and OPL4FM/OPL4PCM.
            return key.Component switch
            {
                ChipVolumeComponent.Whole => label,
                ChipVolumeComponent.Fm => label + "FM",
                ChipVolumeComponent.Ssg => label + "SSG",
                ChipVolumeComponent.Rhythm => label + "RHY",
                ChipVolumeComponent.Adpcm => label + "ADP",
                ChipVolumeComponent.AdpcmA => label + "AA",
                ChipVolumeComponent.AdpcmB => label + "AB",
                ChipVolumeComponent.Pcm => label + "PCM",
                ChipVolumeComponent.Dac => label + "DAC",
                _ => label,
            };
        }
    }
}
