#!/usr/bin/env bash
# ===================================================================
#  Starts the merder level editor.
#
#  Run it from a terminal, or mark it executable (chmod +x start-editor.sh)
#  and launch it from your file manager. Either way it builds first
#  (incremental, so usually a second or two) and then opens the window.
#  Pass "release" to build a Release copy instead.
#
#  Works from any working directory - every path below is derived from
#  where this script lives, not from where you ran it.
# ===================================================================
set -euo pipefail

SCRIPT_DIR=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
APP="$SCRIPT_DIR/Merder.Editor.App"

CONFIG=Debug
case "${1:-}" in
    release|Release) CONFIG=Release ;;
esac

OUT="$APP/bin/$CONFIG/net10.0"
EXE="$OUT/Merder.Editor.App"

die() {
    printf '\n  %s\n\n' "$*" >&2
    exit 1
}

if ! command -v dotnet >/dev/null 2>&1; then
    die "Can't find \"dotnet\" on your PATH.
  Install the .NET SDK from https://dotnet.microsoft.com/download"
fi

# Photino draws through WebKitGTK on Linux. Without it the window dies at
# startup with a native loader error that doesn't explain itself, so say so
# up front rather than letting that be the first clue.
if [ "$(uname -s)" = "Linux" ] && ! ldconfig -p 2>/dev/null | grep -q 'libwebkit2gtk-4\.[01]'; then
    printf '\n  Warning: libwebkit2gtk was not found - the window may fail to open.\n'
    printf '    Debian/Ubuntu:  sudo apt install libwebkit2gtk-4.1-0\n'
    printf '    Fedora:         sudo dnf install webkit2gtk4.1\n'
    printf '    Arch:           sudo pacman -S webkit2gtk-4.1\n\n'
fi

echo "Building the editor ($CONFIG)..."
dotnet build "$APP" --configuration "$CONFIG" --nologo --verbosity quiet

# Same script under Git Bash / MSYS, where the build produces an .exe.
if [ ! -x "$EXE" ] && [ -x "$EXE.exe" ]; then
    EXE="$EXE.exe"
fi

[ -x "$EXE" ] || die "Built, but no executable at:
  $EXE"

echo "Starting..."

# Launched detached, with its working directory set to the output folder, so it
# never inherits whatever folder this script was run from and the terminal is
# free afterwards. Output goes to a log because a detached process has nowhere
# else to put it.
LOG="${TMPDIR:-/tmp}/merder-editor.log"
cd "$OUT"
nohup "$EXE" >"$LOG" 2>&1 &
PID=$!
disown

echo "Running (pid $PID). Log: $LOG"
