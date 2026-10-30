// The page's root view: a scrollable "desktop" on which the player's windows float.
// Windows that size to their content are measured unconstrained, exactly as a native
// SizeToContent window would be, and positioned like macOS places a new window.
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace MDPlayer.UI
{
    // MDPlayer has its own Point/Size compatibility types; these files mean Avalonia's.
    using Point = Avalonia.Point;
    using Size = Avalonia.Size;

    public sealed class WebDesktop : Panel
    {
        private const double DesktopMargin = 24;
        private readonly Dictionary<WindowFrame, Rectangle> modalShades = new();

        public static WebDesktop? Current { get; private set; }

        public WebDesktop()
        {
            Current = this;
            Bind(BackgroundProperty, this.GetResourceObservable("SystemControlBackgroundChromeMediumBrush"));
            // As on macOS, keyboard input always goes to the frontmost window, even after a
            // click on the empty desktop around it.
            PointerPressed += (_, e) =>
            {
                if (e.Source is Visual source && Frames.Any(frame => frame == source || frame.IsVisualAncestorOf(source))) return;
                Frames.LastOrDefault()?.Window.Activate();
            };
        }

        public static Control CreateRoot() => new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            Content = new WebDesktop(),
        };

        public IEnumerable<WindowFrame> Frames => Children.OfType<WindowFrame>();

        public void AddWindow(Window window, Window? owner)
        {
            var frame = new WindowFrame(window);
            if (owner != null)
            {
                // Dialogs are modal: shade and block everything underneath.
                var shade = new Rectangle { Fill = new SolidColorBrush(Color.FromArgb(0x30, 0, 0, 0)) };
                modalShades[frame] = shade;
                Children.Add(shade);
            }
            Children.Add(frame);
            window.Activate();
            Dispatcher.UIThread.Post(window.Activate, DispatcherPriority.Loaded);
        }

        public void RemoveWindow(Window window)
        {
            if (window.Frame is not WindowFrame frame) return;
            Children.Remove(frame);
            frame.Child = null;
            window.Frame = null;
            if (modalShades.Remove(frame, out Rectangle? shade)) Children.Remove(shade);
        }

        public void BringToFront(Window window)
        {
            if (window.Frame is not WindowFrame frame) return;
            // Never raise a window above the dialog that is blocking it.
            if (modalShades.Count > 0 && !modalShades.ContainsKey(frame)) return;
            int index = Children.IndexOf(frame);
            if (index >= 0 && index != Children.Count - 1) Children.Move(index, Children.Count - 1);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            double width = 0, height = 0;
            foreach (Control child in Children)
            {
                if (child is not WindowFrame frame)
                {
                    child.Measure(availableSize);
                    continue;
                }
                frame.Measure(Size.Infinity);
                Point position = PositionOf(frame, availableSize);
                width = Math.Max(width, position.X + frame.DesiredSize.Width + DesktopMargin);
                height = Math.Max(height, position.Y + frame.DesiredSize.Height + DesktopMargin);
            }
            return new Size(width, height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            foreach (Control child in Children)
            {
                if (child is WindowFrame frame)
                {
                    Point position = PositionOf(frame, finalSize);
                    frame.Position = position;
                    frame.Arrange(new Rect(position, frame.DesiredSize));
                }
                else
                {
                    child.Arrange(new Rect(finalSize));
                }
            }
            return finalSize;
        }

        private Point PositionOf(WindowFrame frame, Size area)
        {
            if (!double.IsNaN(frame.Position.X)) return frame.Position;
            Size size = frame.DesiredSize;
            double viewportWidth = double.IsInfinity(area.Width) ? Bounds.Width : area.Width;
            if (frame.Window.Owner?.Frame is WindowFrame owner && !double.IsNaN(owner.Position.X))
            {
                // WindowStartupLocation.CenterOwner
                double x = owner.Position.X + (owner.Bounds.Width - size.Width) / 2;
                double y = owner.Position.Y + Math.Max(0, (owner.Bounds.Height - size.Height) / 3);
                return new Point(Math.Max(DesktopMargin / 2, x), Math.Max(DesktopMargin / 2, y));
            }
            return new Point(Math.Max(DesktopMargin, (viewportWidth - size.Width) / 2), DesktopMargin);
        }
    }
}
