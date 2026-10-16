#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
rm -rf "$ROOT/artifacts" "$ROOT/.packages"
find "$ROOT" -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} +
