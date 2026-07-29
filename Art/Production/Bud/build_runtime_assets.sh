#!/usr/bin/env bash
set -euo pipefail

# Build the approved Bud and ghost-fire concept art into RimWorld's 256 px
# directional texture contract. Every Bud frame is scaled from its complete
# source cell to a fixed 176 px visible width, centered, and planted on the
# same y=235 ground line so swapping materials never changes apparent size.

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
concept_root="$project_root/Art/Concepts/Sprout_AnimationKit_v1/alpha"
bud_sheet="$concept_root/BudAttackSequence.png"
ghostfire_master_root="$project_root/Art/Production/Bud/Ghostfire_v2"
texture_root="$project_root/Textures/Things/Deepcaller_Bud"
icon_root="$project_root/Textures/UI_Deepcaller/Abilities"

mkdir -p \
    "$texture_root/Frames/Closed" \
    "$texture_root/Frames/Flare" \
    "$texture_root/Frames/Recoil" \
    "$icon_root"

build_bud_frame()
{
    local output_path="$1"
    local crop_width="$2"
    local crop_height="$3"
    local crop_x="$4"
    local crop_y="$5"

    magick "$bud_sheet" \
        -crop "${crop_width}x${crop_height}+${crop_x}+${crop_y}" \
        +repage \
        -trim \
        +repage \
        -filter Lanczos \
        -resize 176x \
        -gravity south \
        -background none \
        -extent 256x235 \
        -gravity north \
        -extent 256x256 \
        -depth 8 \
        "$output_path"
}

# The generated sheet is 1619x971: five near-equal columns and three
# near-equal rows. Rows are North, East, South. Runtime idle uses the glowing
# second column; closed, flare, and recoil preserve the full firing sequence
# without baking a duplicate projectile into the body sprite.
for direction_spec in "north 0 324" "east 324 323" "south 647 324"; do
    read -r direction crop_y crop_height <<< "$direction_spec"

    build_bud_frame "$texture_root/Bud_${direction}.png" \
        324 "$crop_height" 324 "$crop_y"
    build_bud_frame "$texture_root/Frames/Closed/BudClosed_${direction}.png" \
        324 "$crop_height" 0 "$crop_y"
    build_bud_frame "$texture_root/Frames/Flare/BudFlare_${direction}.png" \
        323 "$crop_height" 648 "$crop_y"
    build_bud_frame "$texture_root/Frames/Recoil/BudRecoil_${direction}.png" \
        324 "$crop_height" 1295 "$crop_y"
done

# The East flare cell contains one tiny disconnected remnant below its rift
# from the neighboring generated frame. Remove only that 17x4 alpha island.
east_flare="$texture_root/Frames/Flare/BudFlare_east.png"
magick "$east_flare" \
    \( +clone -alpha extract -fill black \
       -draw "rectangle 118,228 143,239" \) \
    -alpha off \
    -compose CopyOpacity \
    -composite \
    "$east_flare"

# Two compact flame variants provide the wobbling/spinning illusion. Their
# production masters share a 1254 px canvas and a fixed leading core. Crop the
# same 850 px registration window from both instead of centering their
# different flame silhouettes; the white-cyan core consequently remains at
# approximately x=128, y=67 in both runtime frames. RimWorld considers the TOP
# of a projectile texture its forward direction.
build_ghostfire_frame()
{
    local input_path="$1"
    local output_path="$2"

    magick "$input_path" \
        -crop 850x850+203+222 \
        +repage \
        -filter Lanczos \
        -resize 256x256! \
        -depth 8 \
        -define png:color-type=6 \
        "$output_path"
}

build_ghostfire_frame \
    "$ghostfire_master_root/GhostfireTailRight_alpha.png" \
    "$texture_root/GhostfireTailUp.png"
build_ghostfire_frame \
    "$ghostfire_master_root/GhostfireTailLeft_alpha.png" \
    "$texture_root/GhostfireTailDown.png"

# Keep the original single-frame path as an oriented compatibility fallback.
cp "$texture_root/GhostfireTailUp.png" "$texture_root/Ghostfire.png"

magick \
    -delay 7 "$texture_root/GhostfireTailUp.png" \
    -delay 7 "$texture_root/GhostfireTailDown.png" \
    -loop 0 \
    "$project_root/Art/Production/Bud/GhostfireSpinPreview.gif"

# The ability icon deliberately reuses the exact South runtime anatomy.
cp "$texture_root/Bud_south.png" "$icon_root/Bud.png"
