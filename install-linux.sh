#!/usr/bin/env bash
# BLACKSITE one-line installer for Linux (ParrotOS, Debian, Ubuntu, Kali, Mint, Fedora, Arch ...) on x86-64 and ARM64.
#
#   curl -fsSL https://raw.githubusercontent.com/unupunct/Blacksite/main/install-linux.sh | bash
#
# Options (pass after `bash -s --`):  --uninstall   remove BLACKSITE      --no-deps   do not try to install system packages
# Environment:                         BLACKSITE_VERSION=v1.0.0  to install a specific release instead of the latest
#
# Installs for the current user only (no root needed except for the optional system packages):
#   ~/.local/share/blacksite   program      ~/.local/bin/blacksite   launcher
#   ~/.local/share/applications/blacksite.desktop   menu entry
set -euo pipefail

REPO="unupunct/Blacksite"
APP_DIR="${XDG_DATA_HOME:-$HOME/.local/share}/blacksite"
BIN_DIR="$HOME/.local/bin"
DESKTOP_DIR="${XDG_DATA_HOME:-$HOME/.local/share}/applications"
ICON_DIR="${XDG_DATA_HOME:-$HOME/.local/share}/icons/hicolor/256x256/apps"
VERSION="${BLACKSITE_VERSION:-latest}"
DEPS=1; UNINSTALL=0
for a in "$@"; do case "$a" in --uninstall) UNINSTALL=1 ;; --no-deps) DEPS=0 ;; esac; done

say()  { printf '\033[1;32m==>\033[0m %s\n' "$*"; }
warn() { printf '\033[1;33mwarning:\033[0m %s\n' "$*" >&2; }
die()  { printf '\033[1;31merror:\033[0m %s\n' "$*" >&2; exit 1; }

if [ "$UNINSTALL" = 1 ]; then
  rm -rf "$APP_DIR" "$BIN_DIR/blacksite" "$DESKTOP_DIR/blacksite.desktop" "$ICON_DIR/blacksite.png" "$HOME/.config/autostart/blacksite.desktop"
  command -v update-desktop-database >/dev/null 2>&1 && update-desktop-database "$DESKTOP_DIR" >/dev/null 2>&1 || true
  say "BLACKSITE removed (your settings in ~/.config/blacksite were kept)."
  exit 0
fi

[ "$(uname -s)" = "Linux" ] || die "this installer is for Linux (Windows: see the README)."
case "$(uname -m)" in
  x86_64|amd64)  ARCH=x64 ;;
  aarch64|arm64) ARCH=arm64 ;;
  *) die "unsupported CPU architecture: $(uname -m) (need x86-64 or ARM64)" ;;
esac

if command -v curl >/dev/null 2>&1; then dl() { curl -fL --progress-bar -o "$2" "$1"; }
elif command -v wget >/dev/null 2>&1; then dl() { wget -q --show-progress -O "$2" "$1"; }
else die "curl or wget is required"; fi
command -v tar >/dev/null 2>&1 || die "tar is required"

# ---- system packages (Avalonia needs X11 + fontconfig; sound uses pacat/pw-cat/aplay, speech uses espeak-ng) ----
if [ "$DEPS" = 1 ] && command -v apt-get >/dev/null 2>&1 && command -v dpkg >/dev/null 2>&1; then
  want=""
  for p in libfontconfig1 libx11-6 libice6 libsm6 libxcursor1 libxrandr2 libxi6 pulseaudio-utils espeak-ng; do
    dpkg -s "$p" >/dev/null 2>&1 || want="$want $p"
  done
  if [ -n "$want" ]; then
    say "Installing system packages:$want"
    if [ "$(id -u)" = 0 ]; then SUDO=""; elif command -v sudo >/dev/null 2>&1; then SUDO="sudo"; else SUDO="none"; fi
    if [ "$SUDO" = "none" ]; then warn "no sudo - please install:$want"
    else
      # shellcheck disable=SC2086
      $SUDO apt-get install -y --no-install-recommends $want >/dev/null 2>&1 \
        || { $SUDO apt-get update -qq >/dev/null 2>&1 && $SUDO apt-get install -y --no-install-recommends $want >/dev/null 2>&1; } \
        || warn "could not install:$want  (continuing - BLACKSITE may lack sound or voice)"
    fi
  fi
elif [ "$DEPS" = 1 ]; then
  warn "not a Debian-based system: make sure fontconfig, libX11, libICE, libSM and (for sound) pulseaudio-utils or pipewire are installed; espeak-ng gives voice."
fi

# ---- download + verify ----
if [ "$VERSION" = "latest" ]; then BASE="https://github.com/$REPO/releases/latest/download"; else BASE="https://github.com/$REPO/releases/download/$VERSION"; fi
TMP="$(mktemp -d)"; trap 'rm -rf "$TMP"' EXIT
FILE="blacksite-linux-$ARCH.tar.gz"
say "Downloading $FILE ($VERSION)"
dl "$BASE/$FILE" "$TMP/$FILE" || die "download failed - is there a release with $FILE at https://github.com/$REPO/releases ?"
if dl "$BASE/SHA256SUMS" "$TMP/SHA256SUMS" >/dev/null 2>&1 && command -v sha256sum >/dev/null 2>&1; then
  want="$(grep " $FILE\$" "$TMP/SHA256SUMS" | cut -d' ' -f1)"
  got="$(sha256sum "$TMP/$FILE" | cut -d' ' -f1)"
  [ -n "$want" ] && [ "$want" = "$got" ] || die "checksum mismatch - refusing to install"
  say "Checksum OK"
else warn "could not verify the checksum"; fi

# ---- install ----
say "Installing to $APP_DIR"
mkdir -p "$TMP/x" "$BIN_DIR" "$DESKTOP_DIR" "$ICON_DIR"
tar -xzf "$TMP/$FILE" -C "$TMP/x"
rm -rf "$APP_DIR"; mkdir -p "$(dirname "$APP_DIR")"
mv "$TMP/x/blacksite" "$APP_DIR"
chmod +x "$APP_DIR/blacksite"
ln -sf "$APP_DIR/blacksite" "$BIN_DIR/blacksite"
[ -f "$APP_DIR/blacksite.png" ] && cp "$APP_DIR/blacksite.png" "$ICON_DIR/blacksite.png"
cat > "$DESKTOP_DIR/blacksite.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=BLACKSITE
GenericName=Tactical Interface Simulator
Comment=Fifteen interactive sci-fi command-centre displays (all data fictional)
Exec=$APP_DIR/blacksite
Icon=blacksite
Terminal=false
Categories=Utility;Game;
StartupWMClass=blacksite
EOF
command -v update-desktop-database >/dev/null 2>&1 && update-desktop-database "$DESKTOP_DIR" >/dev/null 2>&1 || true

say "Done. Start it from the application menu (Utility/Games) or run:  blacksite"
case ":$PATH:" in *":$BIN_DIR:"*) ;; *) warn "$BIN_DIR is not on your PATH yet - log out and in again, or run:  $BIN_DIR/blacksite" ;; esac
echo "    Options:  blacksite --fullscreen --preset hacker    (presets: space hacker surveillance jarvis lockdown satellite reactor cyber deepspace tactical orbital quantum commarray medical facility)"
echo "    Remove:   curl -fsSL https://raw.githubusercontent.com/$REPO/main/install-linux.sh | bash -s -- --uninstall"
