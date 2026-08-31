#!/bin/sh
# Mosaic Cloud - customer setup for macOS and Linux.
#
#   sh install.sh --url wss://cloud.example.com/tunnel --token <token> [--project <unity project path>]
#
# Installs the Unity packages into the project, checks the toolchain, and starts the
# connector. Nothing of the Mosaic pipeline is installed: this machine only ever holds
# the Unity packages and this connector.
set -e

URL=""; TOKEN=""; PROJECT=""
while [ $# -gt 0 ]; do
  case "$1" in
    --url) URL="$2"; shift 2 ;;
    --token) TOKEN="$2"; shift 2 ;;
    --project) PROJECT="$2"; shift 2 ;;
    *) echo "unknown option: $1" >&2; exit 2 ;;
  esac
done
[ -n "$URL" ] && [ -n "$TOKEN" ] || {
  echo "usage: sh install.sh --url wss://<service>/tunnel --token <token> [--project <path>]" >&2
  exit 2
}

echo "== 1/3 checking the toolchain =="
for t in node npm; do
  command -v $t >/dev/null 2>&1 || { echo "missing: $t (install Node 20 or newer)"; exit 1; }
done
node -e 'process.exit(parseInt(process.versions.node) >= 20 ? 0 : 1)' || { echo "Node 20 or newer is required"; exit 1; }
echo "ok  node $(node --version)"

echo "== 2/3 Unity packages =="
if [ -n "$PROJECT" ]; then
  MANIFEST="$PROJECT/Packages/manifest.json"
  [ -f "$MANIFEST" ] || { echo "not a Unity project: $PROJECT"; exit 1; }
  node -e '
    const fs = require("fs");
    const file = process.argv[1];
    const m = JSON.parse(fs.readFileSync(file, "utf8"));
    const want = {
      "com.mosaic.bridge": "https://github.com/MosaicXR-AI/mosaic-bridge.git?path=/packages/com.mosaic.bridge"
    };
    let changed = false;
    for (const [k, v] of Object.entries(want)) {
      if (!m.dependencies[k]) { m.dependencies[k] = v; changed = true; }
    }
    if (changed) {
      fs.writeFileSync(file, JSON.stringify(m, null, 2) + "\n");
      console.log("added the Mosaic Bridge package to the project manifest");
    } else {
      console.log("the project already references the Mosaic Bridge package");
    }
  ' "$MANIFEST"
  echo "Open the project in Unity once so the package imports, then leave the Editor running."
else
  echo "skipped (no --project given). Add com.mosaic.bridge to your Unity project manually,"
  echo "or re-run with --project <path to the Unity project>."
fi

echo "== 3/3 starting the connector =="
DIR=$(cd "$(dirname "$0")" && pwd)
if [ ! -d "$DIR/node_modules" ]; then (cd "$DIR" && npm install --omit=dev >/dev/null 2>&1); fi
if [ ! -f "$DIR/dist/index.js" ]; then (cd "$DIR" && npm install >/dev/null 2>&1 && npx tsc >/dev/null 2>&1); fi
echo "Connecting to $URL"
echo "Leave this window open while you work. Ctrl+C stops it."
exec node "$DIR/dist/index.js" --url "$URL" --token "$TOKEN"
