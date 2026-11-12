#!/bin/bash
set -euo pipefail
task_root="$(cd "$(dirname "$0")/.." && pwd)"
export CLANG_MODULE_CACHE_PATH="${TMPDIR:-/tmp}/koko-swift-clang-cache"
export SWIFTPM_MODULECACHE_OVERRIDE="${TMPDIR:-/tmp}/koko-swift-module-cache"
swift package --package-path "$task_root/macos" --disable-sandbox resolve
swift build --package-path "$task_root/macos" -c release --disable-sandbox
swift test --package-path "$task_root/macos" -c release --disable-sandbox --disable-xctest --enable-swift-testing
bash "$task_root/scripts/build-macos.sh"
"$task_root/artifacts/KOKO Note.app/Contents/MacOS/KOKONoteMac" --self-test > "$task_root/artifacts/macos-ui-test.json"
codesign --verify --strict "$task_root/artifacts/KOKO Note.app"
printf 'resolve: pass | build: pass | test: pass | AppKit integration: pass | signature: pass\n'
