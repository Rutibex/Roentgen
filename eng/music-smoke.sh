#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
"$ROOT/eng/musiclab.sh" validate
"$ROOT/eng/musiclab.sh" compare-seeds
"$ROOT/eng/musiclab.sh" test-transition
