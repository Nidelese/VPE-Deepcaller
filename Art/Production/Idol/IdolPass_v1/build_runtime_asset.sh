#!/usr/bin/env bash
set -euo pipefail

# Build the 256 px Graphic_Single used by Deepcaller_Idol while preserving the
# approved square canvas and bottom-center ground registration.

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../../.." && pwd)"
source_path="$project_root/Art/Production/Idol/IdolPass_v1/DeepcallerIdol_alpha.png"
runtime_path="$project_root/Textures/Things/Deepcaller_Idol.png"

magick -size 256x256 canvas:none \
    \( "$source_path" \
        -filter Lanczos \
        -resize 256x256 \
    \) \
    -geometry +0+16 \
    -composite \
    -strip \
    -depth 8 \
    "$runtime_path"
