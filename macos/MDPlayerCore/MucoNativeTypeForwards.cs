using System.Runtime.CompilerServices;
using MDPlayer.Driver.MUCOM;

// Preserve type identities for tools compiled before the native-core split.
// New builds reference the same public names in MDPlayer.MucoNative.dll.
[assembly: TypeForwardedTo(typeof(MsxMubProfile))]
[assembly: TypeForwardedTo(typeof(MsxSoundMapper))]
