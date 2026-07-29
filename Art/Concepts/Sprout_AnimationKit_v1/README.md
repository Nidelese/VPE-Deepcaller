# Deepcaller Sprout Animation Kit v1

This is a non-runtime concept and production-direction pass. Nothing in `Textures/`
has been replaced.

## Direction convention

- Rows are ordered **North, East, South**.
- RimWorld can mirror the East art for West.
- All creature frames retain a bottom-center rift anchor.

## Sheets

### DirectionalPoseMatrix

Four columns, ordered:

1. Excited — canonical active anatomy
2. Sleepy — natural long-idle/rest pose
3. Spiral — melee anticipation
4. Bud — ranged morph

Three rows are North, East, South.

### ExcitedSleepySequence

Six columns:

1. Alert
2. Relaxed sway
3. Lowering
4. Asleep
5. Sleepy breathing
6. Waking

### SpiralAttackSequence

Five columns:

1. Alert
2. Curl
3. Fully coiled anticipation
4. Whip strike
5. Recoil

### BudAttackSequence

Five columns:

1. Closed
2. Charging
3. Leaking ghost-fire
4. Firing
5. Wilted recovery

### RiftWobbleLoop

Eight standalone frames. The footprint remains approximately fixed while lobes,
sparks, rim brightness, and the dark internal twist travel around the aperture.
This should read as unstable dimensional energy rather than a liquid puddle.

### GhostfireProjectile

Eight frames covering launch seed, flight variations, pre-impact flare, spectral
bloom, and dissipating burst. A production projectile could loop frames 2–5 in
flight and use frames 6–8 for impact.

## File sets

- `keyed/` contains the full-resolution yellow-key generation masters.
- `alpha/` contains the corresponding transparent review assets.

These are concept sheets rather than final sliced runtime frames. Final production
should normalize cell bounds and anchors after the preferred frames are selected.
