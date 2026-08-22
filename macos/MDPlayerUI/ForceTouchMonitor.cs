using System;
using System.Runtime.InteropServices;
using Avalonia.Threading;

namespace MDPlayer.UI
{
    // Keeps the AppKit monitor isolated from the cross-platform transport widget.
    // The native callback runs during local event dispatch on macOS's UI thread.
    internal static class ForceTouchMonitor
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void NativeForceTouchCallback();

        private static readonly NativeForceTouchCallback callback = OnNativeForceTouch;
        private static bool installed;

        internal static event Action? ForceClicked;

        [DllImport("libMDPlayerForceTouch.dylib", CallingConvention = CallingConvention.Cdecl)]
        private static extern void MDPlayerInstallForceTouchMonitor(NativeForceTouchCallback callback);

        [DllImport("libMDPlayerForceTouch.dylib", CallingConvention = CallingConvention.Cdecl)]
        private static extern void MDPlayerConfigureApplication(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string iconPath);

        internal static void ConfigureApplicationIdentity()
        {
            if (!OperatingSystem.IsMacOS()) return;

            string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "MDPlayer4Mac.icns");
            try
            {
                MDPlayerConfigureApplication("MDPlayer4Mac", File.Exists(iconPath) ? iconPath : string.Empty);
            }
            catch (DllNotFoundException)
            {
                // Non-macOS development builds do not produce the AppKit bridge.
            }
        }

        internal static void Install()
        {
            if (installed || !OperatingSystem.IsMacOS()) return;

            try
            {
                MDPlayerInstallForceTouchMonitor(callback);
                installed = true;
            }
            catch (DllNotFoundException)
            {
                // Development on a non-macOS host still retains the normal
                // two-second long press. The release build always includes it.
            }
        }

        private static void OnNativeForceTouch()
        {
            if (Dispatcher.UIThread.CheckAccess())
                ForceClicked?.Invoke();
            else
                Dispatcher.UIThread.Post(() => ForceClicked?.Invoke());
        }
    }
}
