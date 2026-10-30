// The macOS window chrome drawn around a Window inside the page: title bar with traffic
// lights and centred title, rounded corners and shadow. Dragging the title bar moves the
// window and the lower-right corner resizes it (when the window allows resizing).
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace MDPlayer.UI
{
    // MDPlayer has its own Point/Size compatibility types; this file means Avalonia's.
    using Point = Avalonia.Point;
    using Size = Avalonia.Size;

    public sealed class WindowFrame : Decorator
    {
        public const double TitleBarHeight = 28;

        private readonly Border resizeGrip;
        private Point? dragStart;
        private Point dragOrigin;
        private Point? resizeStart;
        private Size resizeOrigin;

        public Window Window { get; }

        // Top-left corner inside the WebDesktop; NaN until first placed.
        public Point Position { get; set; } = new(double.NaN, double.NaN);

        public WindowFrame(Window window)
        {
            Window = window;
            window.Frame = this;

            var titleText = new TextBlock
            {
                [!TextBlock.TextProperty] = window[!Window.TitleProperty],
                FontSize = 13,
                FontWeight = FontWeight.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(70, 0),
                IsHitTestVisible = false,
            };
            titleText.Bind(TextBlock.ForegroundProperty, this.GetResourceObservable("SystemControlForegroundBaseMediumHighBrush"));

            var lights = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Margin = new Thickness(12, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    TrafficLight(Color.FromRgb(0xFF, 0x5F, 0x57), () => { if (window.IsDialog) window.Close(); }),
                    TrafficLight(Color.FromRgb(0xFE, 0xBC, 0x2E), null),
                    TrafficLight(Color.FromRgb(0x28, 0xC8, 0x40), null),
                },
            };

            var titleBar = new Border
            {
                Height = TitleBarHeight,
                Child = new Panel { Children = { titleText, lights } },
            };
            titleBar.Bind(Border.BackgroundProperty, this.GetResourceObservable("SystemControlBackgroundChromeMediumLowBrush"));
            titleBar.PointerPressed += OnTitleBarPressed;
            titleBar.PointerMoved += OnTitleBarMoved;
            titleBar.PointerReleased += (_, _) => dragStart = null;
            DockPanel.SetDock(titleBar, Dock.Top);

            resizeGrip = new Border
            {
                Width = 14,
                Height = 14,
                Background = Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Cursor = new Cursor(StandardCursorType.BottomRightCorner),
                [!IsVisibleProperty] = window[!Window.CanResizeProperty],
            };
            resizeGrip.PointerPressed += OnGripPressed;
            resizeGrip.PointerMoved += OnGripMoved;
            resizeGrip.PointerReleased += (_, _) => resizeStart = null;

            Child = new Border
            {
                CornerRadius = new CornerRadius(10),
                BoxShadow = new BoxShadows(new BoxShadow { OffsetY = 10, Blur = 32, Color = Color.FromArgb(0x70, 0, 0, 0) }),
                Child = new Border
                {
                    CornerRadius = new CornerRadius(10),
                    ClipToBounds = true,
                    BorderThickness = new Thickness(1),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 0x80, 0x80, 0x80)),
                    [!Border.BackgroundProperty] = window[!Window.BackgroundProperty],
                    Child = new Panel
                    {
                        Children =
                        {
                            new DockPanel { Children = { titleBar, window } },
                            resizeGrip,
                        },
                    },
                },
            };
        }

        private static Control TrafficLight(Color color, Action? click)
        {
            var dot = new Ellipse
            {
                Width = 12,
                Height = 12,
                Fill = new SolidColorBrush(color),
                Stroke = new SolidColorBrush(Color.FromArgb(0x30, 0, 0, 0)),
                StrokeThickness = 0.5,
            };
            if (click != null) dot.PointerReleased += (_, e) => { click(); e.Handled = true; };
            return dot;
        }

        private void OnTitleBarPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || WebDesktop.Current == null) return;
            dragStart = e.GetPosition(WebDesktop.Current);
            dragOrigin = Position;
            e.Pointer.Capture((IInputElement?)sender);
            Window.Activate();
        }

        private void OnTitleBarMoved(object? sender, PointerEventArgs e)
        {
            if (dragStart is not Point start || WebDesktop.Current == null) return;
            Vector delta = e.GetPosition(WebDesktop.Current) - start;
            Position = new Point(Math.Max(0, dragOrigin.X + delta.X), Math.Max(0, dragOrigin.Y + delta.Y));
            WebDesktop.Current.InvalidateArrange();
        }

        private void OnGripPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!Window.CanResize || WebDesktop.Current == null) return;
            resizeStart = e.GetPosition(WebDesktop.Current);
            resizeOrigin = Window.Bounds.Size;
            Window.SizeToContent = SizeToContent.Manual;
            e.Pointer.Capture(resizeGrip);
            e.Handled = true;
        }

        private void OnGripMoved(object? sender, PointerEventArgs e)
        {
            if (resizeStart is not Point start || WebDesktop.Current == null) return;
            Vector delta = e.GetPosition(WebDesktop.Current) - start;
            Window.Width = Math.Clamp(resizeOrigin.Width + delta.X, Window.MinWidth, Window.MaxWidth);
            Window.Height = Math.Clamp(resizeOrigin.Height + delta.Y, Window.MinHeight, Window.MaxHeight);
        }
    }
}
