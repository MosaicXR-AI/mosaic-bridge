#!/bin/sh
# Builds Mosaic-Connector.pkg for macOS.
#
#   sh packaging/build-macos-pkg.sh [version]
#
# The result installs one binary to /usr/local/bin and nothing else: no Node, no
# runtime, no per-user state. Configuration happens on first run, and Unity projects
# are added from the app afterwards, because the bridge package belongs to a project
# rather than to the machine.
set -e
VERSION="${1:-0.1.0}"
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(dirname "$HERE")"
BIN="$ROOT/build/index-macos-arm64"
STAGE="$ROOT/build/pkgroot"
OUT="$ROOT/build/Mosaic-Connector-$VERSION.pkg"

[ -f "$BIN" ] || { echo "build the binary first: npm run build && npx pkg dist/index.js --targets node20-macos-arm64 --out-path build"; exit 1; }

rm -rf "$STAGE"
mkdir -p "$STAGE/usr/local/bin"
cp "$BIN" "$STAGE/usr/local/bin/mosaic-connector"
chmod 755 "$STAGE/usr/local/bin/mosaic-connector"

# A postinstall step tells the person what to do next, in Installer's own window.
SCRIPTS="$ROOT/build/pkgscripts"
rm -rf "$SCRIPTS"; mkdir -p "$SCRIPTS"
cat > "$SCRIPTS/postinstall" <<'EOF'
#!/bin/sh
exit 0
EOF
chmod 755 "$SCRIPTS/postinstall"

pkgbuild \
  --root "$STAGE" \
  --scripts "$SCRIPTS" \
  --identifier "ai.mosaicxr.connector" \
  --version "$VERSION" \
  --install-location "/" \
  "$ROOT/build/component.pkg"

cat > "$ROOT/build/distribution.xml" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<installer-gui-script minSpecVersion="1">
  <title>Mosaic Connector</title>
  <organization>ai.mosaicxr</organization>
  <options customize="never" require-scripts="false" hostArchitectures="arm64,x86_64"/>
  <welcome mime-type="text/plain">Mosaic Connector links this machine's Unity Editor to your Mosaic service.

Nothing from the Mosaic pipeline is installed here. This installs one small program that connects outward to the service and forwards commands to your Unity Editor.

After installing, open Terminal and run:

    mosaic-connector setup

It will ask for your service address and access code, then offer to add the Mosaic Bridge package to the Unity projects on this machine.</welcome>
  <conclusion mime-type="text/plain">Installed.

Next: open Terminal and run

    mosaic-connector setup

Then, whenever you are working, leave this running:

    mosaic-connector run</conclusion>
  <pkg-ref id="ai.mosaicxr.connector"/>
  <choices-outline><line choice="default"><line choice="ai.mosaicxr.connector"/></line></choices-outline>
  <choice id="default"/>
  <choice id="ai.mosaicxr.connector" visible="false"><pkg-ref id="ai.mosaicxr.connector"/></choice>
  <pkg-ref id="ai.mosaicxr.connector" version="$VERSION" onConclusion="none">component.pkg</pkg-ref>
</installer-gui-script>
EOF

productbuild \
  --distribution "$ROOT/build/distribution.xml" \
  --package-path "$ROOT/build" \
  "$OUT"

rm -f "$ROOT/build/component.pkg" "$ROOT/build/distribution.xml"
rm -rf "$STAGE" "$SCRIPTS"
echo "built: $OUT"
echo
echo "Unsigned. On a customer Mac, Gatekeeper will ask them to right-click and Open,"
echo "or the package can be signed with a Developer ID Installer certificate:"
echo "  productsign --sign \"Developer ID Installer: NAME\" \"$OUT\" \"\${OUT%.pkg}-signed.pkg\""
