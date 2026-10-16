#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RID="${1:-linux-x64}"
VERSION="${2:-0.21.0-preview}"
OUTPUT="$ROOT/artifacts/publish/$RID"
rm -rf "$OUTPUT"
dotnet publish "$ROOT/src/Roentgen.App/Roentgen.App.csproj" -c Release -r "$RID" --self-contained true -o "$OUTPUT" \
  -p:PublishSingleFile=false -p:PublishReadyToRun=false -p:Version="$VERSION"
python3 "$ROOT/eng/write_build_manifest.py" "$OUTPUT" "$RID" "$VERSION"
