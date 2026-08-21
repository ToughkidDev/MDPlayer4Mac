// Pixel-exact transport button built from the original Windows cc/ch/ci sprite triplets.
// It deliberately uses PixelScreen instead of platform-native button chrome so MDPlayer's
// 16x16 artwork remains crisp at 2x and the normal/hover/pressed states match frmMain.
using System;
using Avalonia.Controls;
using Avalonia.Input;

namespace MDPlayer.UI.Visualizer
{
    public sealed class TransportSpriteButton
    {
        private readonly SpriteAtlas normal;
        private readonly SpriteAtlas hover;
        private readonly SpriteAtlas active;
        private bool enabled = true;
        private bool pointerOver;
        private bool pointerDown;
        private bool selected;

        public PixelScreen Screen { get; } = new();
        public event Action? Click;

        public bool IsEnabled
        {
            get => enabled;
            set
            {
                if (enabled == value) return;
                enabled = value;
                Screen.IsHitTestVisible = value;
                Screen.Opacity = value ? 1.0 : 0.38;
                pointerDown = false;
                Redraw();
            }
        }

        // Pause and currently-running play use the Windows ci* artwork to make state
        // visible without replacing an icon with a text label.
        public bool IsSelected
        {
            get => selected;
            set
            {
                if (selected == value) return;
                selected = value;
                Redraw();
            }
        }

        public TransportSpriteButton(string iconName, string tooltip)
        {
            normal = SpriteAtlas.Load($"Transport/cc{iconName}");
            hover = SpriteAtlas.Load($"Transport/ch{iconName}");
            active = SpriteAtlas.Load($"Transport/ci{iconName}");

            Screen.Init(16, 16, zoom: 2);
            ToolTip.SetTip(Screen, tooltip);
            Screen.PointerEntered += (_, _) => { pointerOver = true; Redraw(); };
            Screen.PointerExited += (_, _) => { pointerOver = false; pointerDown = false; Redraw(); };
            Screen.PointerPressed += OnPointerPressed;
            Screen.PointerReleased += OnPointerReleased;
            Redraw();
        }

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!enabled) return;
            pointerDown = true;
            e.Pointer.Capture(Screen);
            Redraw();
            e.Handled = true;
        }

        private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            bool invoke = enabled && pointerDown && pointerOver;
            pointerDown = false;
            if (e.Pointer.Captured == Screen) e.Pointer.Capture(null);
            Redraw();
            if (invoke) Click?.Invoke();
            e.Handled = true;
        }

        private void Redraw()
        {
            SpriteAtlas source = pointerDown || selected ? active : pointerOver && enabled ? hover : normal;
            Array.Copy(source.Pixels, Screen.Buffer, Math.Min(source.Pixels.Length, Screen.Buffer.Length));
            Screen.Present();
        }
    }
}
