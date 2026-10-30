using System.Reflection;
using System.Text;
using Avalonia;
using Avalonia.Browser;
using Avalonia.Media;

namespace MDPlayer.UI
{
    internal sealed partial class Program
    {
        private static Task Main(string[] args)
        {
            // Some emulator cores print per-instruction traces to stdout; in a browser each
            // line becomes a console.log call that stalls playback.
            Console.SetOut(TextWriter.Null);
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            // Setting.Load locates the app folder from the executable path, which a browser
            // build does not have (Assembly.Location is empty).
            MDPlayer.Application.ExecutablePath = Path.Combine(Common.GetApplicationFolder(), "MDPlayer4Mac");
            InstallRhythmSamples();
            WebPersistence.Start();

            return AppBuilder.Configure<WebApp>()
                .WithInterFont()
                .With(new FontManagerOptions
                {
                    // macOS falls back to its system fonts for Korean and Japanese text and
                    // for families it does not have (e.g. "Consolas"). The browser has no
                    // system fonts, so the same roles are filled by bundled fonts.
                    DefaultFamilyName = "fonts:Inter#Inter",
                    FontFallbacks = new[]
                    {
                        new FontFallback { FontFamily = new FontFamily("avares://MDPlayer4Mac/Assets/Fonts#Noto Sans CJK KR") },
                    },
                })
                .LogToTrace()
                .StartBrowserAppAsync("out");
        }

        // The macOS app ships these beside its executable; FMGen looks for them in the
        // application folder (Common.GetApplicationFolder) when a song folder has none.
        private static void InstallRhythmSamples()
        {
            string folder = Common.GetApplicationFolder();
            Directory.CreateDirectory(folder);
            Assembly assembly = typeof(Program).Assembly;
            foreach (string name in assembly.GetManifestResourceNames().Where(n => n.StartsWith("Rhythm.", StringComparison.Ordinal)))
            {
                using Stream source = assembly.GetManifestResourceStream(name)!;
                using FileStream target = File.Create(Path.Combine(folder, name["Rhythm.".Length..]));
                source.CopyTo(target);
            }
        }
    }

    // Same application setup as macos/MDPlayerUI/App.axaml, hosting MainWindow on the page.
    public sealed class WebApp : Avalonia.Application
    {
        internal static MainWindow? MainWindow { get; private set; }

        public override void Initialize()
        {
            RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Default;
            Styles.Add(new Avalonia.Themes.Fluent.FluentTheme());
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.ISingleViewApplicationLifetime singleView)
            {
                singleView.MainView = WebDesktop.CreateRoot();
                MainWindow = new MainWindow();
                MainWindow.WebInitialize();
                MainWindow.Show();
            }
            base.OnFrameworkInitializationCompleted();
        }
    }
}
