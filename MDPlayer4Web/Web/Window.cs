// In-page stand-in for Avalonia.Controls.Window.
//
// Avalonia.Browser has no windowing platform: constructing an Avalonia Window throws. The
// macOS player's MainWindow and SettingsWindow are declared in namespace MDPlayer.UI as
// `: Window`, and C# resolves a type in the enclosing namespace before one brought in by
// `using Avalonia.Controls;`, so compiling them next to this class makes them derive from
// it without touching their sources. It keeps the Window members those sources use
// (Title, SizeToContent, CanResize, ShowDialog, Close, Closing, StorageProvider ...).
// Like a native window's client area, this control is only the content: WebDesktop puts
// it inside a WindowFrame that draws the macOS title bar around it.
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;

namespace MDPlayer.UI
{
    public class Window : ContentControl
    {
        public static readonly StyledProperty<string?> TitleProperty =
            AvaloniaProperty.Register<Window, string?>(nameof(Title));
        public static readonly StyledProperty<SizeToContent> SizeToContentProperty =
            AvaloniaProperty.Register<Window, SizeToContent>(nameof(SizeToContent));
        public static readonly StyledProperty<bool> CanResizeProperty =
            AvaloniaProperty.Register<Window, bool>(nameof(CanResize), true);

        private TaskCompletionSource<object?>? dialogResult;

        public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
        public SizeToContent SizeToContent { get => GetValue(SizeToContentProperty); set => SetValue(SizeToContentProperty, value); }
        public bool CanResize { get => GetValue(CanResizeProperty); set => SetValue(CanResizeProperty, value); }
        public WindowStartupLocation WindowStartupLocation { get; set; }

        internal WindowFrame? Frame { get; set; }
        internal Window? Owner { get; private set; }
        internal bool IsDialog => dialogResult != null;

        public event EventHandler? Closing;
        public event EventHandler? Closed;

        public IStorageProvider StorageProvider => TopLevel.GetTopLevel(this)!.StorageProvider;

        public Window()
        {
            Focusable = true;
            // Same resources as the Fluent theme's Window control theme.
            Bind(BackgroundProperty, this.GetResourceObservable("SystemControlBackgroundAltHighBrush"));
            Bind(ForegroundProperty, this.GetResourceObservable("SystemControlForegroundBaseHighBrush"));
            Bind(FontFamilyProperty, this.GetResourceObservable("ContentControlThemeFontFamily"));
            Bind(FontSizeProperty, this.GetResourceObservable("ControlContentThemeFontSize"));
            Template = new FuncControlTemplate<Window>((window, scope) =>
            {
                var presenter = new ContentPresenter
                {
                    Name = "PART_ContentPresenter",
                    [!ContentPresenter.ContentProperty] = window[!ContentProperty],
                    [!ContentPresenter.ContentTemplateProperty] = window[!ContentTemplateProperty],
                };
                presenter.RegisterInNameScope(scope);
                return new Border { [!Border.BackgroundProperty] = window[!BackgroundProperty], Child = presenter };
            });
            AddHandler(PointerPressedEvent, (_, _) => Activate(), handledEventsToo: true);
        }

        // A real Window keeps its current size once SizeToContent is switched to Manual and
        // measures its content again when it goes back to WidthAndHeight.
        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == SizeToContentProperty)
            {
                SizeToContent mode = change.GetNewValue<SizeToContent>();
                if (mode == SizeToContent.Manual)
                {
                    if (Bounds.Width > 0) Width = Bounds.Width;
                    if (Bounds.Height > 0) Height = Bounds.Height;
                }
                else
                {
                    if (mode.HasFlag(SizeToContent.Width)) ClearValue(WidthProperty);
                    if (mode.HasFlag(SizeToContent.Height)) ClearValue(HeightProperty);
                }
                WebDesktop.Current?.InvalidateMeasure();
            }
        }

        public void Show() => WebDesktop.Current!.AddWindow(this, null);

        public Task<TResult> ShowDialog<TResult>(Window owner)
        {
            Owner = owner;
            dialogResult = new TaskCompletionSource<object?>();
            WebDesktop.Current!.AddWindow(this, owner);
            return dialogResult.Task.ContinueWith(task => task.Result is TResult result ? result : default!,
                TaskScheduler.FromCurrentSynchronizationContext());
        }

        public Task ShowDialog(Window owner) => ShowDialog<object?>(owner);

        public void Close() => Close(null);

        public void Close(object? dialogResult)
        {
            // The main window cannot be closed in a page; only dialogs go away.
            if (this.dialogResult == null) return;
            Closing?.Invoke(this, EventArgs.Empty);
            WebDesktop.Current?.RemoveWindow(this);
            TaskCompletionSource<object?> pending = this.dialogResult;
            this.dialogResult = null;
            Closed?.Invoke(this, EventArgs.Empty);
            pending.TrySetResult(dialogResult);
            Owner?.Activate();
        }

        public void Activate()
        {
            WebDesktop.Current?.BringToFront(this);
            if (TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is not Visual focused
                || !this.IsVisualAncestorOf(focused))
            {
                Focus();
            }
        }
    }
}
