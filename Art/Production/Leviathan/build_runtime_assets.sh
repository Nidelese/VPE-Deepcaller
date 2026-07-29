#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
project_root="$(cd -- "${script_dir}/../../.." && pwd)"
alpha_root="${script_dir}/v2/alpha"
runtime_root="${project_root}/Textures/Things/Deepcaller_Leviathan"

command -v magick >/dev/null

install -d \
  "${runtime_root}/HeadFrames/Blink" \
  "${runtime_root}/HeadFrames/Focus" \
  "${runtime_root}/HeadFrames/Pleased" \
  "${runtime_root}/FlowerFrames/Blink" \
  "${runtime_root}/FlowerFrames/Focus"

# Every expression was generated on the same 1254-square registration canvas.
# Keep a common crop so swapping an eye expression never changes apparent size
# or makes the body jump by a pixel.
registered_sprite() {
  local source_path="$1"
  local output_path="$2"
  magick "${source_path}" \
    -crop 1150x1150+52+52 +repage \
    -filter Lanczos -resize 256x256 \
    -strip "${output_path}"
}

for direction in south east north; do
  registered_sprite \
    "${alpha_root}/LeviathanHeadIdle_${direction}.png" \
    "${runtime_root}/LeviathanHead_${direction}.png"
  registered_sprite \
    "${alpha_root}/LeviathanFlowerIdle_${direction}.png" \
    "${runtime_root}/LeviathanFlower_${direction}.png"
done

for direction in south east; do
  registered_sprite \
    "${alpha_root}/LeviathanHeadBlink_${direction}.png" \
    "${runtime_root}/HeadFrames/Blink/LeviathanHeadBlink_${direction}.png"
  registered_sprite \
    "${alpha_root}/LeviathanHeadFocus_${direction}.png" \
    "${runtime_root}/HeadFrames/Focus/LeviathanHeadFocus_${direction}.png"
  registered_sprite \
    "${alpha_root}/LeviathanHeadPleased_${direction}.png" \
    "${runtime_root}/HeadFrames/Pleased/LeviathanHeadPleased_${direction}.png"

  registered_sprite \
    "${alpha_root}/LeviathanFlowerBlink_${direction}.png" \
    "${runtime_root}/FlowerFrames/Blink/LeviathanFlowerBlink_${direction}.png"
  registered_sprite \
    "${alpha_root}/LeviathanFlowerFocus_${direction}.png" \
    "${runtime_root}/FlowerFrames/Focus/LeviathanFlowerFocus_${direction}.png"
done

# The back view has no visible primary eyes. Reusing it for expression frames
# avoids inventing a face on the Leviathan's rear while satisfying MultiGraphic.
cp \
  "${runtime_root}/LeviathanHead_north.png" \
  "${runtime_root}/HeadFrames/Blink/LeviathanHeadBlink_north.png"
cp \
  "${runtime_root}/LeviathanHead_north.png" \
  "${runtime_root}/HeadFrames/Focus/LeviathanHeadFocus_north.png"
cp \
  "${runtime_root}/LeviathanHead_north.png" \
  "${runtime_root}/HeadFrames/Pleased/LeviathanHeadPleased_north.png"
cp \
  "${runtime_root}/LeviathanFlower_north.png" \
  "${runtime_root}/FlowerFrames/Blink/LeviathanFlowerBlink_north.png"
cp \
  "${runtime_root}/LeviathanFlower_north.png" \
  "${runtime_root}/FlowerFrames/Focus/LeviathanFlowerFocus_north.png"

# Projectiles use compact square canvases. Trimming first retains their shape,
# while the shared extent gives RimWorld a stable rotation center.
magick "${alpha_root}/SludgeGlob.png" \
  -trim +repage \
  -filter Lanczos -resize '54x54>' \
  -gravity center -background none -extent 64x64 \
  -strip "${runtime_root}/SludgeGlob.png"
magick "${alpha_root}/FlowerStunPearl.png" \
  -trim +repage \
  -filter Lanczos -resize '50x50>' \
  -gravity center -background none -extent 64x64 \
  -strip "${runtime_root}/FlowerStunPearl.png"

echo "Built Leviathan runtime sprites in ${runtime_root}"
