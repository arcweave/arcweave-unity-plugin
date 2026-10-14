#!/usr/bin/env bash
# Builds the plugin packages from the Assets/Arcweave folder committed at a git ref:
#   ArcweaveForUnity.unitypackage  imported with Assets > Import Package > Custom Package
#   ArcweaveForUnity.zip           the Arcweave folder, copied into a project's Assets folder
#
# Usage: build-release-packages.sh [ref] [output directory]
#
# The README and the marketing website link to these file names through
# https://github.com/arcweave/arcweave-unity-plugin/releases/latest/download/<name>,
# so renaming them breaks those links.
set -euo pipefail

ref="${1:-HEAD}"
mkdir -p "${2:-dist}"
out_dir=$(cd "${2:-dist}" && pwd)
name=ArcweaveForUnity

work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT
mkdir "$work/src" "$work/package"

# Only committed files are packaged, so local untracked files never end up in a release.
git archive --format=zip -o "$out_dir/$name.zip" "$ref:Assets" Arcweave Arcweave.meta
git archive "$ref:Assets" Arcweave Arcweave.meta | tar -x -C "$work/src"

cd "$work/src"

# Unity gives assets without a .meta file a new guid on import, which breaks references
# to them when users upgrade the plugin.
while IFS= read -r -d '' path; do
  [ -e "$path.meta" ] || { echo "Assets/$path has no .meta file" >&2; exit 1; }
done < <(find Arcweave ! -name '*.meta' -print0)

# A .unitypackage is a gzipped tar with a folder per asset, named after its guid, holding
# the asset (files only), its .meta file and its path in the project.
while IFS= read -r -d '' meta; do
  path=${meta%.meta}
  guid=$(sed -n 's/^guid: *\([0-9a-f]\{32\}\).*$/\1/p' "$meta")
  [ -n "$guid" ] || { echo "Assets/$meta has no guid" >&2; exit 1; }
  [ -e "$path" ] || { echo "Assets/$meta has no matching asset" >&2; exit 1; }
  entry="$work/package/$guid"
  [ ! -e "$entry" ] || { echo "Assets/$meta reuses guid $guid" >&2; exit 1; }
  mkdir "$entry"
  cp "$meta" "$entry/asset.meta"
  [ -d "$path" ] || cp "$path" "$entry/asset"
  printf 'Assets/%s' "$path" > "$entry/pathname"
done < <(find . -name '*.meta' -printf '%P\0')

cd "$work/package"
tar --sort=name --owner=0 --group=0 --numeric-owner -cf - -- * | gzip -9n > "$out_dir/$name.unitypackage"

echo "Built $out_dir/$name.unitypackage and $out_dir/$name.zip from $ref"
