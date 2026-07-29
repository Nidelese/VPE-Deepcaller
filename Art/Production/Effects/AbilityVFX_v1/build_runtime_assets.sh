#!/usr/bin/env bash
set -euo pipefail

# Crop the approved animation atlases into fixed-size runtime frames.

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../../.." && pwd)"
production_root="$project_root/Art/Production/Effects/AbilityVFX_v1"
runtime_root="$project_root/Textures/Things/Deepcaller_Effects"

mkdir -p \
    "$runtime_root/Riptide" \
    "$runtime_root/Grasp" \
    "$runtime_root/Ink" \
    "$runtime_root/Tideguard"

crop_frame()
{
    local atlas="$1"
    local cell_width="$2"
    local cell_height="$3"
    local column="$4"
    local row="$5"
    local output="$6"
    local x=$(( column * cell_width ))
    local y=$(( row * cell_height ))

    magick "$atlas" \
        -crop "${cell_width}x${cell_height}+${x}+${y}" \
        +repage \
        -filter Lanczos \
        -resize 256x256 \
        -strip \
        -depth 8 \
        "$output"
}

riptide="$production_root/RiptideAtlas_alpha.png"
crop_frame "$riptide" 627 627 0 0 "$runtime_root/Riptide/Riptide_0.png"
crop_frame "$riptide" 627 627 1 0 "$runtime_root/Riptide/Riptide_1.png"
crop_frame "$riptide" 627 627 0 1 "$runtime_root/Riptide/Riptide_2.png"
crop_frame "$riptide" 627 627 1 1 "$runtime_root/Riptide/Riptide_3.png"

grasp="$production_root/GraspAtlas_alpha.png"
crop_frame "$grasp" 512 512 0 0 "$runtime_root/Grasp/Grasp_0.png"
crop_frame "$grasp" 512 512 1 0 "$runtime_root/Grasp/Grasp_1.png"
crop_frame "$grasp" 512 512 2 0 "$runtime_root/Grasp/Grasp_2.png"
crop_frame "$grasp" 512 512 0 1 "$runtime_root/Grasp/Grasp_3.png"
crop_frame "$grasp" 512 512 1 1 "$runtime_root/Grasp/GraspTether.png"
crop_frame "$grasp" 512 512 2 1 "$runtime_root/Grasp/GraspRelease.png"

# The generated Ink sheet used white separator rules despite the requested
# green gutters. Clear only those known gutter bands before exact grid crops;
# every painted asset remains comfortably inside its cell.
ink_clean="$production_root/InkAtlas_alpha_clean.png"
magick "$production_root/InkAtlas_alpha.png" \
    \( +clone \
        -alpha extract \
        -fill black \
        -draw "rectangle 496,0 548,1023 rectangle 990,0 1036,1023 rectangle 0,494 1535,540" \
    \) \
    -alpha off \
    -compose CopyOpacity \
    -composite \
    "$ink_clean"

crop_frame "$ink_clean" 512 512 0 0 "$runtime_root/Ink/InkCore_0.png"
crop_frame "$ink_clean" 512 512 1 0 "$runtime_root/Ink/InkCore_1.png"
crop_frame "$ink_clean" 512 512 2 0 "$runtime_root/Ink/InkCore_2.png"
crop_frame "$ink_clean" 512 512 0 1 "$runtime_root/Ink/InkCore_3.png"
crop_frame "$ink_clean" 512 512 1 1 "$runtime_root/Ink/InkPuff_0.png"
crop_frame "$ink_clean" 512 512 2 1 "$runtime_root/Ink/InkPuff_1.png"

tideguard="$production_root/TideguardAtlas_alpha.png"
crop_frame "$tideguard" 627 627 0 0 "$runtime_root/Tideguard/Tideguard_0.png"
crop_frame "$tideguard" 627 627 1 0 "$runtime_root/Tideguard/Tideguard_1.png"
crop_frame "$tideguard" 627 627 0 1 "$runtime_root/Tideguard/Tideguard_2.png"
crop_frame "$tideguard" 627 627 1 1 "$runtime_root/Tideguard/Tideguard_3.png"
