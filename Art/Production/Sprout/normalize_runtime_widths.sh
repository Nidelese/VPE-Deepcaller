#!/usr/bin/env bash
set -euo pipefail

# RimWorld keeps the draw mesh constant while Graphic_Sprout swaps textures.
# Normalize each approved frame to the same 166 px visible root width so the
# creature no longer appears to inflate/deflate during idle and attack poses.
# Height and the bottom-center anchor remain unchanged.

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
texture_root="$project_root/Textures/Things/Deepcaller_Tentacle"
backup_root="$project_root/Art/Production/Sprout/WidthPass_v1/original"
target_width=166

normalize()
{
    local relative_path="$1"
    local measured_root_width="$2"
    local clear_through_y="${3:--1}"
    local source_path="$texture_root/$relative_path"
    local backup_path="$backup_root/$relative_path"
    local backup_dir="${backup_path%/*}"
    local canvas_width

    mkdir -p "$backup_dir"
    if [[ ! -f "$backup_path" ]]; then
        cp "$source_path" "$backup_path"
    fi

    canvas_width=$(( (256 * target_width + measured_root_width / 2) / measured_root_width ))
    magick "$backup_path" \
        -filter Lanczos \
        -resize "${canvas_width}x256!" \
        -gravity center \
        -background none \
        -extent 256x256 \
        -depth 8 \
        "$source_path"

    # A few source-sheet east poses included a detached upper aperture accent.
    # In play it reads as a second portal floating over the pawn ("the hat").
    # Clear only the empty top band below that accent; the first anatomical
    # pixels in these frames begin safely beneath the frame-specific cutoff.
    if (( clear_through_y >= 0 )); then
        magick "$source_path" \
            \( +clone \
                -alpha extract \
                -fill black \
                -draw "rectangle 0,0 255,${clear_through_y}" \
            \) \
            -alpha off \
            -compose CopyOpacity \
            -composite \
            -depth 8 \
            "$source_path"
    fi
}

normalize "TentacleSprout_north.png" 156
normalize "TentacleSprout_east.png" 146 31
normalize "TentacleSprout_south.png" 166

normalize "SproutFrames/IdleA/TentacleSproutIdleA_north.png" 186
normalize "SproutFrames/IdleA/TentacleSproutIdleA_east.png" 138 45
normalize "SproutFrames/IdleA/TentacleSproutIdleA_south.png" 158

normalize "SproutFrames/IdleB/TentacleSproutIdleB_north.png" 214
normalize "SproutFrames/IdleB/TentacleSproutIdleB_east.png" 138 45
normalize "SproutFrames/IdleB/TentacleSproutIdleB_south.png" 180

# The sleeping sprites place the aperture higher than the other frames, so the
# complete alpha width is their reliable aperture-width measurement.
normalize "SproutFrames/Sleepy/TentacleSproutSleepy_north.png" 214
normalize "SproutFrames/Sleepy/TentacleSproutSleepy_east.png" 214
normalize "SproutFrames/Sleepy/TentacleSproutSleepy_south.png" 214

normalize "SproutFrames/Spiral/TentacleSproutSpiral_north.png" 209
normalize "SproutFrames/Spiral/TentacleSproutSpiral_east.png" 204
normalize "SproutFrames/Spiral/TentacleSproutSpiral_south.png" 198

normalize "SproutFrames/Snap/TentacleSproutSnap_north.png" 146
normalize "SproutFrames/Snap/TentacleSproutSnap_east.png" 163
normalize "SproutFrames/Snap/TentacleSproutSnap_south.png" 168

normalize "SproutFrames/TentacleSproutRecoil_north.png" 198
normalize "SproutFrames/TentacleSproutRecoil_east.png" 191
normalize "SproutFrames/TentacleSproutRecoil_south.png" 182
