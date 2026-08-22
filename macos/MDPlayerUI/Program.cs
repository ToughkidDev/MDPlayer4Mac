using Avalonia;

namespace MDPlayer.UI
{
    internal static class Program
    {
        [STAThread]
        public static void Main(string[] args) => BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);

        // This method is needed for IDE previewer infrastructure too - keep it public and
        // parameterless-callable.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                // Do not overwrite the explicit MDPlayer4Mac process name with
                // Avalonia's default "Avalonia Application" label on macOS.
                .With(new MacOSPlatformOptions { DisableSetProcessName = true })
                .WithInterFont()
                .LogToTrace();
    }
}
