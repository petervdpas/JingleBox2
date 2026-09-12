#!/usr/bin/env bash
#
# Builds one Debian package out of a published payload, and checks what it built.
#
# It was written out inside the workflow, once, for amd64, and adding a second architecture that
# way would have been the whole of it copied: the tree, the launcher, the desktop entry, the
# control file and the rack check, in two places, to be kept in step by hand. That is the fault
# this repository has already paid for with `linux-natives.sh`, where three payloads each named
# the libraries one by one and one of them was forgotten for every release for months.
#
# So there is one spelling of what a package of this program is, and what differs between a
# desktop and a Raspberry Pi is one argument.
#
# **The runtime identifier decides the architecture rather than being told it.** A payload built
# for one machine and labelled for another is a package that installs perfectly and runs nothing,
# and apt has no way of knowing: the arch in the control file is a claim about the files inside,
# so the only safe thing is to work it out from the files' own runtime rather than from a second
# argument that can disagree with the first.
#
# The checking is here rather than in the workflow for the same reason the carrying and the
# checking are one act in `linux-natives.sh`: a check written beside the caller is one a new
# caller does not have.
#
# Usage: build-deb.sh <payload-dir> <rid> <version> [out-dir]

set -euo pipefail

OUT="${1:?usage: build-deb.sh <payload-dir> <rid> <version> [out-dir]}"
RID="${2:?usage: build-deb.sh <payload-dir> <rid> <version> [out-dir]}"
VER="${3:?usage: build-deb.sh <payload-dir> <rid> <version> [out-dir]}"
DIST="${4:-dist}"

APP_NAME="${APP_NAME:-JingleBox2}"
NAME="${DEB_PKG_NAME:-jinglebox2}"

case "$RID" in
  linux-x64)   ARCH="amd64" ;;
  linux-arm64) ARCH="arm64" ;;
  *)
    echo "ERROR: no Debian architecture is known for '$RID'"
    exit 1
    ;;
esac

test -f "$OUT/$APP_NAME" || {
  echo "ERROR: there is no $APP_NAME in $OUT, so there is nothing to package"
  exit 1
}

ROOT="$(mktemp -d)"
PKGDIR="$ROOT/${NAME}_${VER}_${ARCH}"

mkdir -p "$PKGDIR/DEBIAN"
mkdir -p "$PKGDIR/opt/${APP_NAME}"
mkdir -p "$PKGDIR/usr/bin"
mkdir -p "$PKGDIR/usr/share/applications"
mkdir -p "$PKGDIR/usr/share/icons/hicolor/256x256/apps"

cp -a "$OUT/." "$PKGDIR/opt/${APP_NAME}/"
chmod 0755 "$PKGDIR/opt/${APP_NAME}/${APP_NAME}" || true

printf '#!/usr/bin/env bash\nset -euo pipefail\nexec /opt/%s/%s "$@"\n' \
  "$APP_NAME" "$APP_NAME" \
  > "$PKGDIR/usr/bin/$NAME"
chmod 0755 "$PKGDIR/usr/bin/$NAME"

printf '[Desktop Entry]\nType=Application\nName=JingleBox2\nComment=Audio pad launcher\nExec=/usr/bin/%s\nIcon=%s\nTerminal=false\nCategories=Audio;AudioVideo;\n' \
  "$NAME" "$NAME" \
  > "$PKGDIR/usr/share/applications/$NAME.desktop"

if [ -f packaging/fedora/icons/jinglebox2.png ]; then
  cp -a packaging/fedora/icons/jinglebox2.png \
    "$PKGDIR/usr/share/icons/hicolor/256x256/apps/$NAME.png"
fi

INSTALLED_SIZE="$(du -sk "$PKGDIR/opt/${APP_NAME}" | awk '{print $1}')"

# Ubuntu 24.04 and Debian 13 renamed the runtime to libasound2t64 and left libasound2 as a
# virtual package; Raspberry Pi OS is Debian 12 and still has the old name. Either satisfies it,
# so one package installs on both without a second control file.
printf 'Package: %s\nVersion: %s\nSection: sound\nPriority: optional\nArchitecture: %s\nMaintainer: JingleBox2 CI <noreply@github.com>\nInstalled-Size: %s\nDepends: libasound2t64 | libasound2\nDescription: JingleBox2\n Lightweight cross-platform audio pad launcher built with .NET and Avalonia UI.\n' \
  "$NAME" "$VER" "$ARCH" "$INSTALLED_SIZE" \
  > "$PKGDIR/DEBIAN/control"

mkdir -p "$DIST"
DEB="$DIST/${NAME}_${VER}_${ARCH}.deb"

fakeroot dpkg-deb --build "$PKGDIR" "$DEB"

echo "built $DEB"

# What the package says it is for against what is really in it. A self-contained publish carries
# its own runtime, so the executable is the one thing in there that names a machine, and a
# mislabelled package is one that installs and then does nothing whatever.
EXE="$(file -b "$OUT/$APP_NAME" || true)"

case "$ARCH:$EXE" in
  amd64:*x86-64*) ;;
  arm64:*aarch64*) ;;
  *)
    echo "ERROR: a package marked $ARCH was built out of a payload whose program reads: $EXE"
    exit 1
    ;;
esac

echo "OK: the program inside is $ARCH"

# The rack is what the application plays, and it goes missing silently: it is content, so nothing
# compiles it and no test in the suite opens the payload. A count of nought is the check itself
# being broken rather than a package that is empty, which is what once kept a release with no
# machines in it from going out.
EXPECTED="$(find rack -type f \( -name '*.json' -o -name '*.wav' \) | wc -l | tr -d '[:space:]')"
ACTUAL="$(dpkg-deb -c "$DEB" | grep -c " \./opt/${APP_NAME}/rack/.*\.\(json\|wav\)$" || true)"

echo "rack in the package: $ACTUAL/$EXPECTED"

if [ "$EXPECTED" -eq 0 ]; then
  echo "ERROR: nothing on the rack in the source tree; the check itself is broken"
  exit 1
fi

if [ "$ACTUAL" -ne "$EXPECTED" ]; then
  echo "ERROR: the package does not carry every file on the rack"
  exit 1
fi

echo "OK: the rack is in the package"
