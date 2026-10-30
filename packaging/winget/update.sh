#!/usr/bin/env bash
#
# Points the winget manifest in packaging/winget/manifest/ at a published release: its version,
# its installer's address and SHA256, its date and its release notes. Nothing else in the three
# files changes between releases.
#
#   packaging/winget/update.sh            the newest tag
#   packaging/winget/update.sh v2.6.14    that release
#
# The folder is what the first version is submitted from. .github/workflows/winget.yml does that
# itself while winget-pkgs does not have the package yet: point the manifest at the release, push,
# and run that workflow from the Actions tab with the release's tag. It refuses a manifest naming
# a different version. Once winget-pkgs has the package, the same workflow sends every release on
# its own and works the manifest out from the release, so this script is only for that first
# submission and for trying a release from its manifest.
#
# The SHA256 is the one GitHub keeps for the uploaded file. Where an older release has none, the
# installer is downloaded and hashed instead. A release that is not published yet, or has no
# installer, is refused: a manifest pointing at a file that is not there fails winget's own
# validation, and that is a worse place to find out.

set -euo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../.." && pwd)"
DIR="$HERE/manifest"
REPO="petervdpas/JingleBox2"

ASKED="${1:-$(git -C "$ROOT" describe --tags --abbrev=0)}"
VER="${ASKED#v}"
TAG="v$VER"

if ! [[ "$VER" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
  echo "ERROR: $ASKED is not a release version"
  exit 1
fi

ASSET="JingleBox2-Setup-$VER.exe"
URL="https://github.com/$REPO/releases/download/$TAG/$ASSET"

if ! release="$(curl -fsSL "https://api.github.com/repos/$REPO/releases/tags/$TAG")"; then
  echo "ERROR: $TAG is not a published release on GitHub yet"
  exit 1
fi

read -r DATE SHA < <(RELEASE="$release" python3 - "$ASSET" <<'PY'
import json, os, sys
release = json.loads(os.environ["RELEASE"])
asset = next((a for a in release.get("assets", []) if a["name"] == sys.argv[1]), None)
if asset is None:
    print("- -")
else:
    digest = asset.get("digest") or ""
    sha = digest.split(":", 1)[1] if digest.startswith("sha256:") else "-"
    print(release["published_at"][:10], sha)
PY
)

if [ "$DATE" = "-" ]; then
  echo "ERROR: $TAG has no $ASSET"
  exit 1
fi

if [ "$SHA" = "-" ]; then
  echo "GitHub keeps no SHA256 for $ASSET, so it is downloaded to work one out"
  SHA="$(curl -fsSL "$URL" | sha256sum | cut -d' ' -f1)"
fi

SHA="$(printf '%s' "$SHA" | tr 'a-f' 'A-F')"

sed -i -E "s|^PackageVersion: .*|PackageVersion: $VER|" "$DIR"/*.yaml
sed -i -E "s|^ReleaseDate: .*|ReleaseDate: $DATE|; s|^  InstallerUrl: .*|  InstallerUrl: $URL|; s|^  InstallerSha256: .*|  InstallerSha256: $SHA|" "$DIR/PetervdPas.JingleBox2.installer.yaml"
sed -i -E "s|^ReleaseNotesUrl: .*|ReleaseNotesUrl: https://github.com/$REPO/releases/tag/$TAG|" "$DIR/PetervdPas.JingleBox2.locale.en-US.yaml"

for f in "$DIR"/*.yaml; do
  grep -q "^PackageVersion: $VER$" "$f" || { echo "ERROR: $(basename "$f") still names another version"; exit 1; }
done

echo "OK: the manifest is $VER, released $DATE, installer $SHA"
