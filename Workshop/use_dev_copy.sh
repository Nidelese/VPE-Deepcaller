#!/usr/bin/env bash
set -euo pipefail

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
stage_root="${DEEPCALLER_WORKSHOP_STAGING:-/home/elyn/projects/VPE-Deepcaller-WorkshopUpload}"
link_path="/home/elyn/.local/share/Steam/steamapps/common/RimWorld/Mods/VPE-Deepcaller"

if [[ -e "$link_path" && ! -L "$link_path" ]]; then
    echo "Refusing to replace a real directory: $link_path" >&2
    exit 1
fi

current_target="$(readlink "$link_path" 2>/dev/null || true)"
if [[ -n "$current_target" \
      && "$current_target" != "$project_root" \
      && "$current_target" != "$stage_root" ]]; then
    echo "Refusing unexpected symlink target: $current_target" >&2
    exit 1
fi

ln -sfn "$project_root" "$link_path"
echo "RimWorld now sees the live development copy:"
echo "  $link_path -> $project_root"
