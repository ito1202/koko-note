#!/bin/bash
set -euo pipefail
task_root="$(cd "$(dirname "$0")/.." && pwd)"
export CLANG_MODULE_CACHE_PATH="${TMPDIR:-/tmp}/koko-swift-clang-cache"
export SWIFTPM_MODULECACHE_OVERRIDE="${TMPDIR:-/tmp}/koko-swift-module-cache"
swift build --package-path "$task_root/macos" -c release --disable-sandbox
task_binary_dir="$(swift build --package-path "$task_root/macos" -c release --show-bin-path --disable-sandbox)"
task_app="$task_root/artifacts/KOKO Note.app"
mkdir -p "$task_app/Contents/MacOS" "$task_app/Contents/Resources"
cp "$task_binary_dir/KOKONoteMac" "$task_app/Contents/MacOS/KOKONoteMac"
cp "$task_root/macos/Info.plist" "$task_app/Contents/Info.plist"
codesign --force --sign - --identifier com.kokonote.mac.v2 "$task_app"
printf 'Built: %s\n' "$task_app"
