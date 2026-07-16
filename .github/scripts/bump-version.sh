#!/usr/bin/env bash
set -euo pipefail

Bump="${1:-}"
if [[ -z "$Bump" ]]; then
  echo "Usage: $0 <patch|minor|major>" >&2
  exit 1
fi

case "$Bump" in
  patch|minor|major) ;;
  *)
    echo "Invalid bump type: $Bump (expected patch, minor, or major)" >&2
    exit 1
    ;;
esac

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

PROPS_PATH="$REPO_ROOT/Directory.Build.props"
MANIFEST_PATH="$REPO_ROOT/src/Xvm.Blitz.Windows.Client.UI/app.manifest"
WXS_PATH="$REPO_ROOT/src/Xvm.Blitz.Windows.Client.Installer/Xvm Blitz.wxs"

if [[ ! -f "$PROPS_PATH" ]]; then
  echo "File not found: $PROPS_PATH" >&2
  exit 1
fi

props_content="$(cat "$PROPS_PATH")"

if [[ ! "$props_content" =~ \<Version\>([0-9]+)\.([0-9]+)\.([0-9]+)\</Version\> ]]; then
  echo "Version not found in Directory.Build.props" >&2
  exit 1
fi

major="${BASH_REMATCH[1]}"
minor="${BASH_REMATCH[2]}"
patch="${BASH_REMATCH[3]}"
current="${major}.${minor}.${patch}"

case "$Bump" in
  major)
    major=$((major + 1))
    minor=0
    patch=0
    ;;
  minor)
    minor=$((minor + 1))
    patch=0
    ;;
  patch)
    patch=$((patch + 1))
    ;;
esac

version_text="${major}.${minor}.${patch}"
manifest_version="${version_text}.0"

echo "Bumping version ($Bump): $current -> $version_text"

replace_in_file() {
  local file="$1"
  local expression="$2"
  local tmp
  tmp="$(mktemp)"
  sed -E -e "$expression" "$file" > "$tmp"
  mv "$tmp" "$file"
}

tmp="$(mktemp)"
sed -E \
  -e "s/<Version>[0-9]+\.[0-9]+\.[0-9]+<\/Version>/<Version>${version_text}<\/Version>/" \
  -e "s/<InformationalVersion>[0-9]+\.[0-9]+\.[0-9]+<\/InformationalVersion>/<InformationalVersion>${version_text}<\/InformationalVersion>/" \
  "$PROPS_PATH" > "$tmp"
mv "$tmp" "$PROPS_PATH"

if [[ ! -f "$MANIFEST_PATH" ]]; then
  echo "File not found: $MANIFEST_PATH" >&2
  exit 1
fi

replace_in_file "$MANIFEST_PATH" \
  "s/assemblyIdentity version=\"[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+\"/assemblyIdentity version=\"${manifest_version}\"/"

if [[ -f "$WXS_PATH" ]]; then
  replace_in_file "$WXS_PATH" \
    "s/Version=\"[0-9]+\.[0-9]+\.[0-9]+\"/Version=\"${version_text}\"/"
fi

if [[ -n "${GITHUB_OUTPUT:-}" ]]; then
  echo "VERSION=$version_text" >> "$GITHUB_OUTPUT"
fi

echo "New version: $version_text"
