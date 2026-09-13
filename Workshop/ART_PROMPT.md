# Workshop hero art

Mode: Codex built-in image generation, with local deterministic typography,
cropping and downscaling for the final Steam assets. The original stencil-style
title was replaced after live thumbnail review with Noto Serif Display
SemiCondensed Black so every letter in `DEEPCALLER` remains unambiguous at
Steam's 640×360 display size.

Reference images:

- `/tmp/deepcaller_workshop_refs.png`: authoritative runtime identity and
  palette reference assembled from the Idol, Sprout, Bud, Leviathan, flower
  and ability effects.
- `Textures/UI_Deepcaller/Tree/DeepcallerTree.png`: authoritative mood and
  painterly style reference.

Prompt:

> Use case: ads-marketing
>
> Asset type: Steam Workshop mod preview hero, wide 16:9
>
> Input images: Image 1 is the authoritative identity and palette reference
> for the Deepcaller Idol, Sprout, Bud, Leviathan head and flower, plus its
> magical effects. Image 2 is the authoritative mood and painterly style
> reference.
>
> Primary request: Create a polished wide key art scene for the RimWorld mod
> Vanilla Psycasts Expanded — Deepcaller. Preserve the exact cute cosmic-ocean
> creature identity from the references: a large round kawaii Leviathan head
> with huge luminous cyan eyes as the central focal character, emerging from a
> glowing abyssal rift; two living tentacle arms curl inward to frame it. In
> the foreground, show the one-eyed Idol at center, with the small Sprout and
> closed Bud flanking it, and one eye-flower peeking in. Suggest the Riptide
> whirlpool, living ink mist, and a thin water-shield ring as magical accents
> without clutter.
>
> Scene/backdrop: deep midnight abyss with subtle underwater currents and
> drifting bioluminescent motes; terrain-neutral, no biome-specific ground.
>
> Style/medium: premium painterly cartoon game key art matching the cute,
> glossy navy, cyan-biological-light, and violet-accented runtime art; kawaii
> with a gentle cosmic-horror theme, inviting rather than frightening.
>
> Composition/framing: exact wide 16:9 landscape; strong readable silhouette
> at tiny Workshop-thumbnail size; Leviathan centered slightly right; reserve
> a broad clean dark area on the upper-left and lower-left for title typography
> to be added later; foreground creatures fully visible, no cropping through
> faces.
>
> Lighting/mood: cyan light rising from the rift, soft violet rim light,
> magical and mysterious, delightful awe.
>
> Constraints: no text, no letters, no logos, no UI, no border, no watermark.
> Do not redesign the creatures. Preserve the Leviathan's two huge expressive
> eyes, forehead cyan dots and ring markings, rounded mantle lobes, and glowing
> puddle-rift. Preserve the one-eyed Idol and distinct Sprout and Bud
> silhouettes. No gore, no realistic horror, no humans, no terrain tile, no
> weapons.

Generated source:

`Art/Production/Workshop/Deepcaller_WorkshopHero_v1_untitled.png`

Final assets:

- `About/Preview.png` — 640×360, below Steam's 1 MB preview limit.
- `Workshop/Assets/Deepcaller_WorkshopHero_1600x900.png`
- `Workshop/Assets/Deepcaller_DescriptionBanner_1280x320.png`
- `Workshop/Assets/Deepcaller_FeatureStrip_1280x320.png`

Preserved first-pass typography:

- `Art/Production/Workshop/Deepcaller_WorkshopHero_v1_stencil.png`
- `Art/Production/Workshop/Deepcaller_WorkshopPreview_v1_stencil.png`
- `Art/Production/Workshop/Deepcaller_DescriptionBanner_v1_stencil.png`

## Optional-support mascot art

Mode: Codex built-in image generation on a chroma-key background, followed by
local chroma removal and deterministic banner composition.

Reference images:

- `/tmp/deepcaller_workshop_refs.png`: authoritative Deepcaller runtime
  identity and palette reference.
- `Workshop/Assets/Deepcaller_WorkshopHero_1600x900.png`: authoritative
  Workshop rendering and finish reference.

Prompt:

> Create a single tiny kawaii Deepcaller Leviathan mascot as a clean game-art
> cutout. Match the reference identity closely: rounded navy-blue mantle made
> of soft lobes, huge luminous cyan eyes, cyan forehead dots and rings, violet
> nodules, and a small glowing puddle-rift beneath it. Give it an
> extra-hopeful, irresistibly beggy expression without making it look
> distressed. Two little tentacle arms hold an oversized soft dark-navy busker
> hat out toward the viewer for optional coins. Put a few ordinary silver
> fantasy coins inside; one prominent silver coin has a clear cute bite taken
> out of its edge like a cookie. Friendly, whimsical, lightly cosmic-horror,
> glossy painterly cartoon game art. One centered character only, fully
> visible, isolated on flat uniform pure chroma green #00ff00. No text,
> letters, logo, watermark, human hands, currency symbols, cryptocurrency,
> extra creatures, ground scenery, cast shadow, gore, or frightening horror.

Generated keyed source:

`Art/Production/Workshop/Deepcaller_PatreonMascot_keyed.png`

Final assets:

- `Workshop/Assets/Deepcaller_PatreonMascot.png` — transparent cutout.
- `Workshop/Assets/Deepcaller_OptionalSupport_1280x320.png` — optional-support
  banner with deterministic typography.
