# Deepcaller visual bible

## Thesis

The Deepcaller does not summon separate sea creatures. One colossal intelligence
buried beneath the map expresses temporary sensory and feeding organs through
earth, stone, and flesh.

Canonical concept: `Concepts/Deepcaller_VisualBible_v1.png`.

## Production rules

- Camera: orthographic-like RimWorld three-quarter top-down view.
- Light: restrained cold upper-left light; cyan light appears only at psychic
  seams, apertures, eyes, and wounds.
- Flesh: near-black blue-green with bruised violet shadows and damp highlights.
- Suckers and ocelli: corpse-pale or bone-white, sparse and irregular.
- Ground contact: a black aperture surrounded by dry broken earth. Never use a
  clean oval shadow or freestanding creature base.
- Anatomy: asymmetric, scarred, and uncertain. No paired cartoon eyes, faces,
  smiles, orderly sucker rows, or mascot proportions.
- Motion: slow biological pressure punctuated by sudden violence. Avoid buoyant
  bouncing and continuously busy loops.

## Sprite contract

- Working source cells: 512 x 512 pixels.
- Runtime textures: 256 x 256 RGBA PNG.
- Direction suffixes: `_south`, `_east`, `_north`; west mirrors east.
- Subject scale and the center of the ground aperture remain fixed between all
  frames belonging to a stage.
- Preserve transparent padding. Do not trim frames independently: that moves the
  pawn's apparent ground contact during animation.
- Idle loops should be irregular and slow. Attack, emergence, growth, and return
  animations may be fast but must settle back onto the same pivot.

## Sprout v1

Production source: `Production/Sprout/Sprout_SourceSheet_v1.png`.
Alpha master: `Production/Sprout/Sprout_SourceSheet_v1_alpha.png`.
Timing preview: `Production/Sprout/Sprout_IdlePreview.gif`.

The top row supplies the runtime directional idle sprites. The lower row is the
first recoil/breath key pose and remains deliberately separate until the pawn
animation renderer is wired in. This prevents an unfinished global, synchronized
flipbook from shipping accidentally.

## Generation prompt

The Sprout source was generated with the built-in image-generation workflow,
using the canonical concept as a reference. It requested a 3-by-2 sheet on a
flat magenta key: south/east/north across the top, matching compressed recoil
poses below, with fixed scale, pivot, markings, lighting, and earth rupture.
