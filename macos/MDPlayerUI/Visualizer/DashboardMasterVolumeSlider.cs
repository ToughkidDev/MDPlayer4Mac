// Compact master fader for the player dashboard. It uses the same Windows rFader sheet
// as the mixer view, keeping the transport area pixel-art rather than introducing a native
// platform slider next to the sprite buttons.
using System;
using Avalonia.Controls;
using Avalonia.Input;

namespace MDPlayer.UI.Visualizer
{
    public sealed class DashboardMasterVolumeSlider
    {
        private const int Width = 16;
        // rFader's original vertical slit is 56px: one cap, five rail tiles, one cap.
        // Keeping that native shape avoids squeezing the sprite into a malformed rail.
        private const int Height = 56;
        private const int FaderX = 4;
        private const int KnobHeight = 13;
        private const int Travel = Height - KnobHeight;
        // The dashboard is a quick-access control. Keep its useful range compact while
        // the full mixer continues to expose MDPlayer's wider -192dB attenuation range.
        private const int MinimumVolume = -60;
        private const int MaximumVolume = 20;

        private readonly SpriteAtlas fader = SpriteAtlas.Load("rFader");
        private bool enabled;
        private bool dragging;
        private int volume;

        public PixelScreen Screen { get; } = new();
        public event Action<int>? ValueChanged;

        public bool IsEnabled
        {
            get => enabled;
            set
            {
                if (enabled == value) return;
                enabled = value;
                Screen.IsHitTestVisible = value;
                Screen.Opacity = value ? 1.0 : 0.38;
            }
        }

        public DashboardMasterVolumeSlider()
        {
            Screen.Init(Width, Height, zoom: 1);
            Screen.PointerPressed += OnPointerPressed;
            Screen.PointerMoved += OnPointerMoved;
            Screen.PointerReleased += OnPointerReleased;
            Screen.PointerWheelChanged += OnPointerWheelChanged;
            ToolTip.SetTip(Screen, "마스터 볼륨");
            Redraw();
        }

        public void SetValue(int value)
        {
            int clamped = Math.Clamp(value, MinimumVolume, MaximumVolume);
            if (volume == clamped) return;
            volume = clamped;
            Redraw();
        }

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!enabled) return;
            dragging = true;
            e.Pointer.Capture(Screen);
            SetFromPointer(e.GetPosition(Screen).Y);
            e.Handled = true;
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (!dragging || e.Pointer.Captured != Screen) return;
            SetFromPointer(e.GetPosition(Screen).Y);
        }

        private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            dragging = false;
            if (e.Pointer.Captured == Screen) e.Pointer.Capture(null);
        }

        private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            if (!enabled || e.Delta.Y == 0) return;
            SetFromUser(Math.Clamp(volume + Math.Sign(e.Delta.Y), MinimumVolume, MaximumVolume));
            e.Handled = true;
        }

        private void SetFromPointer(double displayY)
        {
            if (Screen.Bounds.Height <= 0) return;
            double nativeY = displayY * Height / Screen.Bounds.Height;
            SetFromUser(VolumeAtPosition(nativeY - KnobHeight / 2.0));
        }

        private void SetFromUser(int value)
        {
            int clamped = Math.Clamp(value, MinimumVolume, MaximumVolume);
            if (volume == clamped) return;
            volume = clamped;
            Redraw();
            ValueChanged?.Invoke(volume);
        }

        // Reserve a short top segment for positive gain and devote the rest of the travel
        // to the dashboard's quick-access -60dB..0dB attenuation range.
        private static int PositionForVolume(int value)
        {
            const int zeroDbPosition = 8;
            return value <= 0
                ? zeroDbPosition + (int)Math.Round(-value * (Travel - zeroDbPosition) / -(double)MinimumVolume)
                : Math.Max(0, zeroDbPosition - (int)Math.Round(value * zeroDbPosition / (double)MaximumVolume));
        }

        private static int VolumeAtPosition(double position)
        {
            const int zeroDbPosition = 8;
            double clamped = Math.Clamp(position, 0, Travel);
            return clamped >= zeroDbPosition
                ? (int)Math.Round(-(clamped - zeroDbPosition) * -MinimumVolume / (Travel - zeroDbPosition))
                : (int)Math.Round((zeroDbPosition - clamped) * MaximumVolume / zeroDbPosition);
        }

        private void Redraw()
        {
            Array.Fill(Screen.Buffer, unchecked((int)0xff000000));

            // The unrotated rFader sprites are already vertical: top cap, five tiled
            // rail segments, bottom cap, then the original 8x13 master knob.
            Screen.DrawIntArray(FaderX, 0, fader.Pixels, fader.Width, 16, 0, 8, 8);
            for (int y = 8; y < Height - 8; y += 8)
            {
                Screen.DrawIntArray(FaderX, y, fader.Pixels, fader.Width, 16, 8, 8, 8);
            }
            Screen.DrawIntArray(FaderX, Height - 8, fader.Pixels, fader.Width, 24, 0, 8, 8);

            int knobY = PositionForVolume(volume);
            Screen.DrawIntArray(FaderX, knobY, fader.Pixels, fader.Width, 0, 0, 8, 13);
            Screen.Present();
        }
    }
}
