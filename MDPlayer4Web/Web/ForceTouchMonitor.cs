// Web stand-in for macos/MDPlayerUI/ForceTouchMonitor.cs, which bridges AppKit's Force
// Touch events. Browsers have no equivalent, so the Play button keeps its two-second
// long press (TransportSpriteButton) as the only way to arm automatic play.
namespace MDPlayer.UI
{
    internal static class ForceTouchMonitor
    {
#pragma warning disable CS0067 // never raised in a browser
        internal static event Action? ForceClicked;
#pragma warning restore CS0067

        internal static void ConfigureApplicationIdentity() { }

        internal static void Install() { }
    }
}
