#!/usr/bin/env bash
#
# Builds artifacts/jff-agent-runner_<version>_amd64.deb.
# Requires the .NET 10 SDK and dpkg-deb, so run it on Linux.
#
# Usage: ./utils/build-deb.sh 1.2.3

set -euo pipefail

VERSION="${1:-}"
if [[ ! "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
    echo "Usage: $0 <major.minor.patch>" >&2
    exit 1
fi

if ! command -v dpkg-deb >/dev/null; then
    echo "dpkg-deb was not found. Run this script on Debian or Ubuntu." >&2
    exit 1
fi

PACKAGE="jff-agent-runner"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUTPUT_DIR="$ROOT/artifacts"
STAGE="$(mktemp -d)"
trap 'rm -rf "$STAGE"' EXIT

dotnet publish "$ROOT/jff-agent/jff-agent.csproj" \
    -c Release \
    -r linux-x64 \
    --self-contained false \
    -p:UseAppHost=false \
    -p:Version="$VERSION" \
    -o "$STAGE/opt/$PACKAGE"

mkdir -p "$STAGE/DEBIAN" "$STAGE/usr/bin"

cat > "$STAGE/usr/bin/$PACKAGE" <<EOF
#!/bin/sh
exec dotnet /opt/$PACKAGE/jff-agent.dll "\$@"
EOF

# Permissions end up in the package, so they must not depend on the build machine's umask.
find "$STAGE" -type d -exec chmod 755 {} +
find "$STAGE" -type f -exec chmod 644 {} +
chmod 755 "$STAGE/usr/bin/$PACKAGE"

# The app uses the shared ASP.NET Core framework (see jff-agent.runtimeconfig.json),
# so dotnet-runtime-10.0 alone is not enough. gh is only recommended because it may
# already be installed from outside apt, such as a snap.
cat > "$STAGE/DEBIAN/control" <<EOF
Package: $PACKAGE
Version: $VERSION
Section: utils
Priority: optional
Architecture: amd64
Installed-Size: $(du -sk "$STAGE/opt" | cut -f1)
Depends: aspnetcore-runtime-10.0, git, tmux
Recommends: gh
Maintainer: Josemberg Ferreira <josembergff@users.noreply.github.com>
Homepage: https://github.com/josembergff/jff-agent-runner
Description: Delegates GitHub Issues to AI coding agents in tmux sessions
 A terminal app that hands GitHub Issues to the agent CLIs already installed
 and authenticated on the server, each one in its own tmux session.
EOF

mkdir -p "$OUTPUT_DIR"
dpkg-deb --build --root-owner-group "$STAGE" "$OUTPUT_DIR/${PACKAGE}_${VERSION}_amd64.deb"
