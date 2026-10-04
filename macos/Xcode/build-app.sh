#!/bin/bash
#
# Run Script phase of macos/MDPlayer4Mac.xcodeproj.
#
# MDPlayer4Mac is an Avalonia (.NET) app, so Xcode cannot compile it itself. The Xcode
# target only provides the .app wrapper (Info.plist, icon, code signing); this script
# publishes macos/MDPlayerUI with the .NET SDK and copies the output into
# Contents/MacOS, the same layout .github/workflows/release-macos.yml produces.

set -euo pipefail

if [ -z "${SRCROOT:-}" ] || [ -z "${TARGET_BUILD_DIR:-}" ]; then
    echo "error: build-app.sh must be run from Xcode (SRCROOT/TARGET_BUILD_DIR not set)." >&2
    exit 1
fi

CSPROJ="$SRCROOT/MDPlayerUI/MDPlayerUI.csproj"

# --- Driver submodules -------------------------------------------------------
if [ ! -f "$SRCROOT/ThirdParty/mucomDotNET/Common_NET5/Common_NET6.csproj" ]; then
    echo "error: macos/ThirdParty submodules are not checked out. Run 'git submodule update --init --recursive' in the repository root." >&2
    exit 1
fi

# --- .NET SDK ------------------------------------------------------------------
# Xcode does not inherit the login shell's PATH, so look in the usual install
# locations as well. Set DOTNET in the scheme's environment to override.
DOTNET_BIN=""
for candidate in \
    "${DOTNET:-}" \
    "$(command -v dotnet 2>/dev/null || true)" \
    /usr/local/share/dotnet/dotnet \
    /usr/local/bin/dotnet \
    /opt/homebrew/bin/dotnet \
    "$HOME/.dotnet/dotnet"; do
    if [ -n "$candidate" ] && [ -x "$candidate" ]; then
        DOTNET_BIN="$candidate"
        break
    fi
done
if [ -z "$DOTNET_BIN" ]; then
    echo "error: .NET SDK (dotnet) not found. Install .NET SDK 8 or later from https://dotnet.microsoft.com/download." >&2
    exit 1
fi

# --- Runtime identifier ----------------------------------------------------------
read -r -a ARCH_LIST <<< "${ARCHS:-arm64}"
if [ "${#ARCH_LIST[@]}" -ne 1 ]; then
    echo "error: build one architecture at a time (ARCHS='${ARCHS}'); dotnet publish cannot produce a universal binary." >&2
    exit 1
fi
case "${ARCH_LIST[0]}" in
    arm64)  RID="osx-arm64" ;;
    x86_64) RID="osx-x64" ;;
    *)
        echo "error: unsupported architecture '${ARCH_LIST[0]}'." >&2
        exit 1
        ;;
esac

case "${CONFIGURATION:-Debug}" in
    Release)
        DOTNET_CONFIGURATION="Release"
        # Same as the release workflow: no .pdb files in the bundle.
        EXTRA_PROPERTIES=("--property:DebugType=None")
        ;;
    *)
        DOTNET_CONFIGURATION="Debug"
        EXTRA_PROPERTIES=()
        ;;
esac

# --- Publish ---------------------------------------------------------------------
PUBLISH_DIR="$TARGET_TEMP_DIR/dotnet-publish"
rm -rf "$PUBLISH_DIR"

# Xcode exports hundreds of build settings as environment variables, and MSBuild
# reads environment variables as properties case-insensitively (e.g. Xcode's
# TARGETNAME would override MSBuild's $(TargetName) for every project). Run the
# .NET SDK with a minimal environment instead.
CLEAN_ENV=(
    "HOME=$HOME"
    "PATH=$(dirname "$DOTNET_BIN"):/usr/bin:/bin:/usr/sbin:/sbin:/usr/local/bin:/opt/homebrew/bin"
    "TMPDIR=${TMPDIR:-/tmp}"
    "LANG=${LANG:-en_US.UTF-8}"
)
for name in USER LOGNAME DEVELOPER_DIR DOTNET_ROOT NUGET_PACKAGES DOTNET_CLI_TELEMETRY_OPTOUT; do
    if [ -n "${!name:-}" ]; then
        CLEAN_ENV+=("$name=${!name}")
    fi
done

# Avalonia 12's XAML source generator needs Roslyn 4.14, which first shipped in
# .NET SDK 9.0.300. With an older SDK the generator is skipped silently and the
# build fails with dozens of CS0103 errors for named XAML controls.
SDK_VERSION="$(cd "$SRCROOT" && env -i "${CLEAN_ENV[@]}" "$DOTNET_BIN" --version)"
IFS=. read -r SDK_MAJOR SDK_MINOR SDK_PATCH <<< "${SDK_VERSION%%-*}"
if [ "$SDK_MAJOR" -lt 9 ] || { [ "$SDK_MAJOR" -eq 9 ] && [ "$SDK_MINOR" -eq 0 ] && [ "$SDK_PATCH" -lt 300 ]; }; then
    echo "error: .NET SDK $SDK_VERSION is too old for Avalonia 12. Install .NET SDK 9.0.300 or later (10 recommended)." >&2
    exit 1
fi

echo "Publishing $CSPROJ ($DOTNET_CONFIGURATION, $RID) with .NET SDK $SDK_VERSION ($DOTNET_BIN)"
env -i "${CLEAN_ENV[@]}" "$DOTNET_BIN" publish "$CSPROJ" \
    --configuration "$DOTNET_CONFIGURATION" \
    --runtime "$RID" \
    --self-contained true \
    --property:PublishSingleFile=true \
    ${EXTRA_PROPERTIES[@]+"${EXTRA_PROPERTIES[@]}"} \
    --output "$PUBLISH_DIR"

# --- Copy into the bundle -------------------------------------------------------
APP_MACOS_DIR="$TARGET_BUILD_DIR/$EXECUTABLE_FOLDER_PATH"
rm -rf "$APP_MACOS_DIR"
mkdir -p "$APP_MACOS_DIR"
ditto "$PUBLISH_DIR" "$APP_MACOS_DIR"

if [ ! -x "$APP_MACOS_DIR/$EXECUTABLE_NAME" ]; then
    echo "error: dotnet publish did not produce '$EXECUTABLE_NAME'. Check AssemblyName in MDPlayerUI.csproj." >&2
    exit 1
fi

# --- Sign nested files -------------------------------------------------------------
# Xcode signs the bundle (and its main executable) after this phase, but codesign
# refuses to seal a bundle that contains unsigned code. Sign everything else in
# Contents/MacOS (dylibs and the data files published next to the executable) first,
# like the release workflow does.
if [ "${CODE_SIGNING_ALLOWED:-YES}" = "YES" ]; then
    SIGN_IDENTITY="${EXPANDED_CODE_SIGN_IDENTITY:-}"
    if [ -z "$SIGN_IDENTITY" ]; then
        SIGN_IDENTITY="-"
    fi
    find "$APP_MACOS_DIR" -type f ! -path "$APP_MACOS_DIR/$EXECUTABLE_NAME" \
        -exec codesign --force --sign "$SIGN_IDENTITY" --timestamp=none {} +
fi
