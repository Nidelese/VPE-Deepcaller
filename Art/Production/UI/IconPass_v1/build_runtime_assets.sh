#!/usr/bin/env bash
set -euo pipefail

# Derive the shipped Deepcaller UI from the approved production masters.
# The ability atlas is an exact 4x3 grid of 362 px cells.

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../../.." && pwd)"
production_root="$project_root/Art/Production/UI/IconPass_v1"
ability_root="$project_root/Textures/UI_Deepcaller/Abilities"
focus_root="$project_root/Textures/UI_Deepcaller/Foci"
tree_root="$project_root/Textures/UI_Deepcaller/Tree"
atlas="$production_root/DeepcallerAbilityAtlas_alpha.png"

mkdir -p "$ability_root" "$focus_root" "$tree_root"

crop_icon()
{
    local filename="$1"
    local column="$2"
    local row="$3"
    local x=$(( column * 362 ))
    local y=$(( row * 362 ))

    magick "$atlas" \
        -crop "362x362+${x}+${y}" \
        +repage \
        -filter Lanczos \
        -resize 128x128 \
        -strip \
        -depth 8 \
        "$ability_root/$filename.png"
}

# Atlas order, left-to-right and top-to-bottom.
crop_icon "Lash" 0 0
crop_icon "Grasp" 1 0
crop_icon "Riptide" 2 0
crop_icon "RaiseIdol" 3 0

crop_icon "InkVeil" 0 1
crop_icon "Consume" 1 1
crop_icon "Bud" 2 1
crop_icon "DeepHoard" 3 1

crop_icon "Overgrowth" 0 2
crop_icon "Symbiote" 1 2
crop_icon "LeviathansWake" 2 2
crop_icon "Tideguard" 3 2

magick "$production_root/EatCorpses_alpha.png" \
    -filter Lanczos \
    -resize 128x128 \
    -strip \
    -depth 8 \
    "$project_root/Textures/UI_Deepcaller/EatCorpses.png"

magick "$production_root/AbyssalFocus_alpha.png" \
    -filter Lanczos \
    -resize 128x128 \
    -strip \
    -depth 8 \
    "$focus_root/Abyssal.png"

# Preserve the existing VPE background contract. The master is 2:3, so fill
# the slightly narrower target and crop symmetrically rather than stretching.
magick "$production_root/DeepcallerUnlockTile_master.png" \
    -filter Lanczos \
    -resize "950x1515^" \
    -gravity center \
    -extent 950x1515 \
    -strip \
    -depth 8 \
    "$tree_root/DeepcallerTree.png"
