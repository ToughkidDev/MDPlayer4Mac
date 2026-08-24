// Pixel-exact transport button built from the original Windows cc/ch/ci sprite triplets.
// It deliberately uses PixelScreen instead of platform-native button chrome so MDPlayer's
// 16x16 artwork remains crisp at 2x and the normal/hover/pressed states match frmMain.
using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using MDPlayer.UI;

namespace MDPlayer.UI.Visualizer
{
    public sealed class TransportSpriteButton
    {
        private SpriteAtlas normal = null!;
        private SpriteAtlas hover = null!;
        private SpriteAtlas active = null!;
        private bool enabled = true;
        private bool pointerOver;
        private bool pointerDown;
        private bool selected;
        private bool redAlert;
        private bool longPressTriggered;
        private bool forceTouchLongPressEnabled;
        private bool allowLongPressWhenDisabled;
        private readonly DispatcherTimer longPressTimer = new() { Interval = TimeSpan.FromSeconds(2) };

        public PixelScreen Screen { get; } = new();
        public event Action? Click;
        public event Action? LongPressed;

        // Play uses this while no song is loaded: it remains visually disabled and
        // short clicks still do nothing, but a long press / Force Click may arm
        // automatic play before files are added.
        public bool AllowLongPressWhenDisabled
        {
            get => allowLongPressWhenDisabled;
            set
            {
                if (allowLongPressWhenDisabled == value) return;
                allowLongPressWhenDisabled = value;
                Screen.IsHitTestVisible = enabled || allowLongPressWhenDisabled;
            }
        }

        // Only the Play button opts in. Other transport controls preserve their
        // regular click action even when a user Force Clicks on their icon.
        public void EnableForceTouchLongPress()
        {
            if (forceTouchLongPressEnabled) return;
            forceTouchLongPressEnabled = true;
            ForceTouchMonitor.ForceClicked += OnForceClicked;
            ForceTouchMonitor.Install();
        }

        public bool IsEnabled
        {
            get => enabled;
            set
            {
                if (enabled == value) return;
                enabled = value;
                Screen.IsHitTestVisible = value || allowLongPressWhenDisabled;
                Screen.Opacity = value ? 1.0 : 0.38;
                pointerDown = false;
                longPressTimer.Stop();
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

        // Used by the Play button while automatic play is armed. It is intentionally
        // independent of IsSelected, which continues to represent normal playback state.
        public bool IsRedAlert
        {
            get => redAlert;
            set
            {
                if (redAlert == value) return;
                redAlert = value;
                Redraw();
            }
        }

        // Loop and random playback share one dashboard position. Swap all three Windows
        // sprite states together so hover/pressed feedback remains faithful to the source.
        public void SetIcon(string iconName, string tooltip)
        {
            normal = SpriteAtlas.Load($"Transport/cc{iconName}");
            hover = SpriteAtlas.Load($"Transport/ch{iconName}");
            active = SpriteAtlas.Load($"Transport/ci{iconName}");
            ToolTip.SetTip(Screen, tooltip);
            Redraw();
        }

        public TransportSpriteButton(string iconName, string tooltip)
        {
            Screen.Init(16, 16, zoom: 2);
            SetIcon(iconName, tooltip);
            Screen.PointerEntered += (_, _) => { pointerOver = true; Redraw(); };
            Screen.PointerExited += (_, _) =>
            {
                pointerOver = false;
                pointerDown = false;
                longPressTimer.Stop();
                Redraw();
            };
            Screen.PointerPressed += OnPointerPressed;
            Screen.PointerReleased += OnPointerReleased;
            longPressTimer.Tick += OnLongPressTimerTick;
            Redraw();
        }

        // Keyboard shortcuts use the identical click path as a pointer release, while
        // retaining the button's normal enabled/disabled state.
        public bool InvokeClick()
        {
            if (!enabled) return false;
            Click?.Invoke();
            return true;
        }

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!enabled && !allowLongPressWhenDisabled) return;
            pointerDown = true;
            longPressTriggered = false;
            longPressTimer.Start();
            e.Pointer.Capture(Screen);
            Redraw();
            e.Handled = true;
        }

        private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            bool invoke = enabled && pointerDown && pointerOver && !longPressTriggered;
            pointerDown = false;
            longPressTimer.Stop();
            if (e.Pointer.Captured == Screen) e.Pointer.Capture(null);
            Redraw();
            if (invoke) Click?.Invoke();
            e.Handled = true;
        }

        private void OnLongPressTimerTick(object? sender, EventArgs e)
        {
            longPressTimer.Stop();
            TryInvokeLongPress();
        }

        private void OnForceClicked()
        {
            if (forceTouchLongPressEnabled)
                TryInvokeLongPress();
        }

        private void TryInvokeLongPress()
        {
            if ((!enabled && !allowLongPressWhenDisabled) || !pointerDown || longPressTriggered) return;
            longPressTriggered = true;
            longPressTimer.Stop();
            LongPressed?.Invoke();
            Redraw();
        }

        private void Redraw()
        {
            SpriteAtlas source = pointerDown || selected ? active : pointerOver && enabled ? hover : normal;
            Array.Copy(source.Pixels, Screen.Buffer, Math.Min(source.Pixels.Length, Screen.Buffer.Length));
            if (redAlert)
            {
                // Keep the dark button background intact while remapping the visible icon
                // pixels to red, making the armed automatic-play state unmistakable.
                for (int i = 0; i < Screen.Buffer.Length; i++)
                {
                    int pixel = Screen.Buffer[i];
                    int red = (pixel >> 16) & 0xff;
                    int green = (pixel >> 8) & 0xff;
                    int blue = pixel & 0xff;
                    int brightness = Math.Max(red, Math.Max(green, blue));
                    if (brightness < 48) continue;
                    Screen.Buffer[i] = unchecked((int)0xff000000) | (0xff << 16)
                        | ((brightness / 5) << 8) | (brightness / 5);
                }
            }
            Screen.Present();
        }
    }
}
