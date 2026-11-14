#!/usr/bin/env bash
# Builds artifacts/jff-agent-runner-web_<version>_amd64.deb.
set -euo pipefail
VERSION="${1:-}"
if [[ ! "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then echo "Usage: $0 <major.minor.patch>" >&2; exit 1; fi
if ! command -v dpkg-deb >/dev/null; then echo "dpkg-deb was not found. Run this script on Debian or Ubuntu." >&2; exit 1; fi
PACKAGE="jff-agent-runner-web"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUTPUT_DIR="$ROOT/artifacts"
STAGE="$(mktemp -d)"
trap 'rm -rf "$STAGE"' EXIT

dotnet publish "$ROOT/jff-agent-runner-web/jff-agent-runner-web.csproj" \
    -c Release -r linux-x64 --self-contained false -p:UseAppHost=false \
    -p:Version="$VERSION" -o "$STAGE/opt/$PACKAGE"

mkdir -p "$STAGE/DEBIAN" "$STAGE/usr/bin"
cat > "$STAGE/usr/bin/$PACKAGE" <<EOF
#!/bin/sh
exec dotnet /opt/$PACKAGE/jff-agent-runner-web.dll "\$@"
EOF
find "$STAGE" -type d -exec chmod 755 {} +
find "$STAGE" -type f -exec chmod 644 {} +
chmod 755 "$STAGE/usr/bin/$PACKAGE"

cat > "$STAGE/DEBIAN/control" <<EOF
Package: $PACKAGE
Version: $VERSION
Section: web
Priority: optional
Architecture: amd64
Installed-Size: $(du -sk "$STAGE/opt" | cut -f1)
Depends: aspnetcore-runtime-10.0, git, tmux
Recommends: gh
Maintainer: Josemberg Ferreira <josembergff@users.noreply.github.com>
Homepage: https://github.com/josembergff/jff-agent-runner
Description: Web interface for jff-agent-runner
 Blazor web interface for managing projects, agents and GitHub issue executions.
EOF

mkdir -p "$OUTPUT_DIR"
dpkg-deb --build --root-owner-group "$STAGE" "$OUTPUT_DIR/${PACKAGE}_${VERSION}_amd64.deb"
