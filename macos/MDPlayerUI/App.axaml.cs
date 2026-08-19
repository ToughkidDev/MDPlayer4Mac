using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace MDPlayer.UI
{
    // Explicitly qualified as Avalonia.Application (not a bare "using Avalonia" + unqualified
    // Application) because this file lives in namespace MDPlayer.UI, which is nested under
    // MDPlayer - and MDPlayer.CompatShims.cs already declares its own `static class Application`
    // (a WinForms Application.ExecutablePath shim for the ported engine code). C#'s namespace
    // lookup finds that enclosing-namespace type before it considers a `using` import, so an
    // unqualified `Application` here silently bound to the wrong (static, non-derivable) type.
    public partial class App : Avalonia.Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow();
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
