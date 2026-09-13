# VPE — Deepcaller

Area-defense summoner path for Vanilla Psycasts Expanded (RimWorld 1.6).
Fantasy: Illaoi / Nagakabouros — call tentacles from the deep, keep the Idol fed,
control the ground around your home.

## Physical force and non-organic combat — 2026-09-12

Version 1.2.0. This section supersedes older Riptide
terrain and target-filter notes below; the 1.1.0 progression systems remain.

- Grasp is the clean tactical hold. Riptide is calamity: all loose physical
  objects in the cast radius can be caught, including allies, caster, neutral
  animals, summons, vehicles, corpses and item stacks. Its fixed roster is
  captured before effects begin; dropped loot, new corpses and wreckage never
  join that cast or take its direct collision damage. Buildings/plants stay
  anchored but can be struck. Casts with no projectiles do not mine anything.
- Initial acceleration is multiplied by starting distance / cast radius:
  strongest at the included outer edge, zero at the exact center. An original
  center occupant turns 180 degrees without translation, injury or stun.
  Velocity integrates acceleration and determines actual displacement. Pressure
  is D for structures/mining and 3 + 0.4D for bodies/debris. Damage is
  `0.6 * pressure * effectiveMass/70 * closingSpeed²`. Normal armor
  handles each side separately. Breaking rock also recoils onto the projectile;
  it must survive to continue. Co-moving objects do not crash. Slow impacts
  are mild, while opposing motion is worse. Acceleration ends at the center
  plane; velocity remains. Air drag applies only afterward, exponentially at
  `0.02 / max(0.25, (mass/70)^(1/3))` per tick. Collisions transfer momentum
  to surviving movable targets, including people protected from pickup.
  Before center, the god replenishes collision losses; intact obstacles still
  block movement and both sides still take damage. After center, ordinary
  impulse exchange with restitution 0.15 removes most impact energy. A paid,
  sated wall favor explicitly stops the projectile while preserving the wall.
- Let normalized mass m = kilograms/70 and authority q = D/(D+800).
  Base acceleration is `(1 + log2(1+D/80)) / max(1,m)^(1.15-0.65q)`; multiply
  by initial radial force and purchased acceleration, then divide by 25 to
  obtain cells/tick². The simulation sweeps up to 32 substeps per tick, with
  an eight-cell/tick integration ceiling. Weight gives modest impact
  protection early and makes late impacts worse for equal run-up. Contact between two movable
  objects uses twice their reduced mass; an anchored obstacle absorbs the
  projectile's mass. Relative speed uses simulated velocities or observed
  ordinary movement; the old cadence/run-up proxy is no longer authoritative.
- A saved cast actor owns movement and collisions outside pawn health ticks.
  It uses actual Vehicle Framework mass (including cargo), the full vehicle
  footprint and the framework's teleport/path/occupancy notifications. Vehicle
  Framework remains optional. Natural-rock hits use Mining damage for normal
  yield, fog and roof handling. Unsupported roofs can collapse.
- Free radius follows the existing curve through (2,160 devotion, 16 cells),
  then gains two cells per devotion doubling up to 32. Damage is bounded to
  1 billion; invalid/extreme devotion cannot overflow damage or indexing.
  At 8,000 devotion: roughly 19.8 free radius. An edge-starting 70 kg projectile after three clear
  cells hits stationary rock for roughly 8,820 before taking 3,530 raw recoil. A 450 kg
  vehicle takes longer to accelerate, but after seven cells can break plasteel.
  A tiny gold bar needs much greater devotion to become a useful mining tool.
- Three independent, one-time favors: spare our walls (walls, doors and full
  structural barriers); spare our people (only hostile non-vehicle pawns are
  pulled); spare our possessions (player vehicles, player corpses, explicitly
  owned items and items accepted in player stockpiles/shelves stay grounded).
  A home-area mark or haul order does not claim debris. Unclaimed chunks fly.
  People and possessions favors prevent pickup, never collision damage. A
  protected colonist hit by a thrown enemy still takes injury; furniture and
  turrets do not inherit the wall favor. All three require a separate purchase
  and the casting faction's sated idol on the target map. Satiety uses
  the existing three-day hunger grace. Conditions are checked during the cast:
  loss of favor can engage originally captured friendlies; no new targets are
  enrolled. Favor returning ends their direct acceleration, preserving existing
  momentum. Buying one grants neither
  the other purchases nor unconditional protection. Tooltips/shop show status.
- Acceleration ranks add 10% of base force, plus 10% every fifth rank. Their
  benefit is real acceleration and the resulting collision velocity. At 5,000
  devotion, separate area and distance purchases become available. Each rank
  adds `4*D/(D+5000)` radius cells or `8*D/(D+5000)` cast cells. Purchases raise
  the eventual limits rather than just approaching a fixed limit sooner.
  Actual reach cannot exceed the map diagonal; purchases stop when the current
  line covers this map. Area snapshots and large outlines avoid radial tables.
  See `BALANCE.md` for all prices, benchmarks, assumptions and examples.
- Gold and some resource stacks have no engine HP. Riptide tracks their
  collision wear against declared durability for the cast, so they can break.
  Captured stacks cannot absorb or merge into other stacks until the cast ends;
  fresh loot therefore cannot inherit a captured object's motion or damage.
  Save data prunes destroyed references, preserves the IDs of captured objects
  against vanilla map compression, and restores velocity and subcell position.
- Riptide, Ink Veil and Grasp explicitly accept mechs/entities without psychic
  sensitivity. Tentacle combat and CC use hostile-body rules independently of
  the food rules. Known VPE/RimWorld of Magic stone, metal and non-flesh golem
  definitions are recognized even where legacy flesh metadata says organic.
- An eighth cultivation entry, "Buds shoot non-organics", is a one-time unlock
  costing 250 base gold with the usual devotion discount (8 gold at D=8,000).
  It applies to all existing/future player Buds on every map. Non-organic
  ghostfire impacts use Blunt through the normal shield/armor pipeline;
  organic impacts retain Burn. Direct targeting and piercing use the unlock.
  Its enum value is appended, preserving the seven existing saved rank slots.
- Consume, idol offerings and tentacle corpse meals still require edible
  flesh. Non-organic enemies can contribute combat growth without becoming
  food. Leviathan movement cultivation stays excluded; a chaotic Riptide can
  catch the player's summons, including the Leviathan.

Build, pure-math/material checks and XML checks are automated. Engine behavior
and the live save/load cases are listed in `Tests/Progression/README.md`.

### Consume and the permanent hoard

Implemented after the Riptide momentum and real game save/load checks passed.
Consume's raw heat includes remaining part health, pawn and gear market value,
combat power, melee damage factor, and installed Isekai levels. Devotion gives
a divisor `1+(D/800)²`; purchased burn reduction supplies a separate divisor.
The complete victim roster and heat bill are frozen before any offering raises
Devotion. Overflow devours the caster after the offerings. The radius is zero
until purchased; each rank adds exactly one tile, starting at 2,000 Devotion.
The area strain multiplier `1+radius²/25` rewards deliberate purchase choices.
All nearby edible pawns except the caster are offerings, including allies.
Non-organics remain inedible. Five-tile and ten-tile casts have no blanket
safety guarantee; boss power can overwhelm ordinary late-game protection.

The silver buyback curve is preserved through 2,000 Devotion, then its quote
continues falling with a divisor `D/2000`. Hunger still worsens the bargain.
Four permanent gold lines have their own hoard window: deeper bargains,
salvage from corpse offerings, repair on recovery, and corpse-taint cleansing.
Salvage and cleansing are one-time unlocks; bargaining and repair repeat.
Deep Hoard's existing passive still preserves gear from living Consume victims.
Repairs occur after payment; taint removal does not remove biocoding or raise
quality. Items cost at least one silver each. See `BALANCE.md` for calibration.

## Progression pass — 2026-09-11

This section supersedes the historical implementation notes below.

- Ordinary melee tentacles start as Sprouts and mature at 2 / 6 / 15 growth.
  Damage dealt to hostile prey earns growth for its contributing tentacle;
  one core-part HP budget per victim is shared by all attackers and never
  refills when that victim heals. Corpses give 1.5 growth per body size before
  feeding cultivation. Summoned deep creatures cannot be meals or XP targets.
- Graspers drag and constrict prey within five cells, Crushers cleave within
  a 1.5-cell area, and Colossi mark a 2.5-cell area for 45 ticks before slamming.
  These special attacks have an eight-second cooldown and affect hostiles only.
- Mature meals heal injuries, replenish active Tideguard energy (never bypass
  EMP reset), and extend remaining life. No healthy, charged, newly summoned
  Colossus eats without a benefit. A summon can gain at most one additional
  original lifespan; each meal also cannot exceed its original remaining life.
- Cultivation uses gold physically inside the idol's shadow. Seven independent
  lines: Bud damage/fire rate/bolt speed; tentacle feeding/regeneration/special
  attacks/movement. Purchased ranks belong to the player's game-wide brood,
  remain on other maps when the idol moves, and survive its loss. Passive raw
  devotion scaling still follows the idol-on-this-map rules of existing powers.
- Price: ceil(baseGold × 1.6^rank / (1 + devotion / 250)), minimum one gold.
  Base prices are 20 / 25 / 15 / 20 / 20 / 30 / 25 respectively. Devotion is
  retained. Price evaluation uses logarithms; values too high for the gold
  transaction are unavailable until discounted, never wrapped into cheap ranks.
  Devotion and withdrawn devotion now use doubles, preserving old numeric saves.
- Effects are additive relative to the devotion-derived baseline: +12% per
  Bud/special-attack rank, +15% feeding/regeneration, +10% movement. Every fifth
  rank also adds 10% of baseline and celebrates a milestone. At rank five,
  damage unlocks line piercing, fire rate paired volleys, bolt speed spectral
  trails/armor penetration, regeneration one regrown missing part per meal.
  Later milestones improve those effects; melee feeding/attack/movement lines
  receive their additional baseline bonus.
- Buds use a fractional firing clock rather than the vanilla ten-tick poll.
  Output is limited to three volleys (six bolts) per second and physical speed
  to 180 cells/s; excess cadence or speed multiplies impact damage. Piercing
  affects at most three hostiles behind the primary impact, within three cells.
- The cultivation window shows current → next effects, local gold, retained
  devotion, and a pinned goal's required additional devotion. The latest offering
  receipt retains even sub-gold price reductions without notification spam.
- Idol awakenings at 10 / 30 / 75 / 175 / 400 / 900 / 2,000 devotion change its
  title, living rings, sound and digestion. Beyond 2,000 each doubling gives a
  further Depth, with digestion batches bounded at 32 to protect frame time.
- Movement cultivation is gated to exactly `Deepcaller_Tentacle`. Buds,
  Leviathan heads, arms and flowers never receive it or the melee growth comp.
- Old idol/withdrawn Bud ranks are imported monotonically into global ranks.
  Old tentacles retain their earned biological stage and reconcile growth,
  graphics and shield severity on spawn/load without free healing.

Validation: `dotnet build Source/Deepcaller/Deepcaller.csproj --no-restore`;
`dotnet run --project Tests/Progression/Progression.csproj --no-restore`;
XML validation (including the MoveSpeed stat patch) during upload staging.
The author reported the in-game playtest complete on 2026-09-12 and authorized
the 1.1.0 release. The playtest checklist remains in Tests/Progression/README.md
for future regression checks.

## Core loop

1. Summon rooted tentacle pawns that lash (melee) and grasp (immobilize) enemies.
2. Tentacles **evolve** through four stages: **Sprout → Grasper → Crusher → Colossus**.
3. Evolution is **layered** (decided 2026-07-02):
   - **Idol devotion** (colony progression): the Idol of the Deep has a persistent
     devotion level, raised by corpses in its shadow and living sacrifices via
     Consume. It strengthens the god's powers and discounts gold cultivation;
     it never skips a new tentacle's growth story.
   - **Feeding** (in-combat progression): each tentacle individually advances stages
     by dealing damage and consuming corpses during its lifetime.

## Tech decisions

- **Tentacles are pawns** (custom PawnKindDef/ThingDef race), movement locked to 0
  via hediff — free melee AI, health, targeting. Same approach as VPE Necropath
  skeletons (`VanillaPsycastsExpanded.Ability_SpawnSkeleton` is the reference).
- Evolution stage = hediff with severity tiers, or stage comp on the pawn; stage
  swap adjusts body size, melee verbs, HP scale.
- Grasp = immobilize hediff on the victim (breaks after damage threshold or when
  the tentacle dies). Anomaly's devourer grapple is prior art (user owns all DLC).
- Idol = building (ThingDef) with a comp tracking devotion; meditation focus.
- C# assembly needed for: evolution comp/hediff logic, Idol devotion comp,
  grasp mechanics, Overgrowth/Leviathan. Summons/pulls/clouds mostly reusable
  from VPE/VEF classes.
- Build: dotnet SDK (not yet installed — `pacman -S dotnet-sdk`), target net472,
  reference game DLLs via Krafs.Rimworld.Ref NuGet + Lib.Harmony.

## Tree layout (draft)

| Lv | Ability | Effect | Impl |
|----|---------|--------|------|
| 1  | Lash | Summon rooted tentacle, short duration, melee slams | XML + spawn class |
| 1  | Grasp | Tentacle erupts under target: anchored in place, breaks on damage | C# |
| 2  | Riptide | AoE pull toward target point | C# (Cyclone/Gravcaster prior art) |
| 2  | Ink Veil | Lingering accuracy-debuff cloud | mostly XML (gas/fleck) |
| 3  | Idol of the Deep | Place idol: devotion tracker, extends nearby tentacle duration, meditation focus | C# comp |
| 3  | Consume | Tentacles devour corpses: extend duration + feed devotion/stage | C# |
| 4  | Symbiote | Graft tentacle onto caster: melee buff + counter-grab | C# hediff |
| 4  | Overgrowth | All active tentacles +1 stage immediately | C# |
| 5  | Leviathan's Wake | Summon THE LEVIATHAN: an octopus — 8 tentacles in fixed positions around a central head that shoots poison sludge, all sharing ONE single giant health pool (Evelyn, 2026-07-02) | C# |

Prereq chains, order, psyfocus/entropy costs TBD during XML pass.

## Def naming

Prefix everything `Deepcaller_` (defNames are permanent once saves exist; display
labels are free to change). Path defName: `Deepcaller_Path`.

## Status / next steps

- [x] Project scaffold, About.xml, git repo (symlinked into game Mods folder)
- [x] PsycasterPathDef + placeholder tree background texture (PIL script in scratchpad; bg 950x1515, icons 128x128)
- [x] Tentacle race ThingDef/PawnKindDef (Sprout stage; inherits `VPE_UndeadBase`; fights via `CompProperties_InitialMentalState`→`VPE_Manhunter`, expires via `CompProperties_DieAfterPeriod`, `CompProperties_DoesntFlee`)
- [x] Lash ability XML + C# `Ability_SummonTentacle` (VEF `Ability_Spawn` can't spawn pawns — uses `ThingMaker`; VEF `CompSummonOnSpawn` is hostile-only. Own class mirrors VPE `Ability_SpawnSkeleton`, pawnkind configurable via `AbilityExtension_SummonPawn`)
- [x] Build DLL: dotnet-sdk installed; `dotnet build Source/Deepcaller` → `1.6/Assemblies/Deepcaller.dll`
- [x] First in-game smoke test passed (tree renders, Lash summons, territorial targeting works)
- [x] Grip-on-hit: Deepcaller_Crush damage + Deepcaller_Gripped slow hediff
- [x] Custom think tree (organic turret: JobGiver_AIFightEnemies-based; manhunter
      states can't target animals — vanilla filters to ToolUser+ intelligence)
- [x] Evolution v1 (per-tentacle feeding): stages are LifeStageDefs driven by
      biological age (Sprout 0y → Grasper 1y → Crusher 2y → Colossus 3y);
      CompTentacleGrowth accumulates feed points (kills via Pawn.Kill Harmony
      patch, corpses via consume job) and bumps AgeBiologicalTicks. LifeStageDef
      natively scales bodySize/health/meleeDamage/MoveSpeed; pawnkind lifeStages
      scale drawSize. Acquire radius grows with stage (32 + 10/stage) via
      JobGiver_TentacleFight — Evelyn's idea: "the deep sees farther as it feeds"
- [ ] Rooting: tentacles currently slither; anchor later if playtests want it
- [x] Idol of the Deep v1: level-2 psycast Raise Idol (VEF Ability_SpawnBuilding,
      XML-only placement); CompIdolDevotion consumes corpses in radius 12 every
      ~1h (devotion += bodySize; legacy thresholds 5/15/30 = levels 1-3); Harbinger-tree-style gizmo zones the
      radius as a corpse stockpile so vanilla hauling delivers offerings
- [x] One god, movable (Evelyn, 2026-07-03): re-casting Raise Idol MOVES the
      existing idol (devotion/hunger/hoard transplanted into the fresh spawn,
      old shell vanished) instead of duplicating. Nomad-proof for Odyssey
      (Evelyn's insight): the cast searches ALL maps, and Building_DeepIdol
      overrides Notify_MyMapRemoved to withdraw the god's state into the
      game component when its map is discarded — the next Raise Idol
      anywhere restores it, devotion unbroken. Corpse stockpile zones do
      NOT move with it (recreate via gizmo)
- [ ] Idol polish: per-level visual states, offering ritual, maybe devotion
      decay / acquire-radius aura
- [x] Idol as meditation focus (Evelyn, 2026-07-02; built 2026-07-03):
      MeditationFocusDef `Deepcaller_Deep` ("abyssal", no requirements —
      vanilla treats a focus type with no enablers anywhere as usable by
      everyone) + vanilla CompProperties_MeditationFocus on the idol.
      Strength = 0.20 base stat + FocusStrengthOffset_IdolDevotion curve
      (0→+0, 100→+0.08, 500→+0.14, 1000→+0.20, 2000→+0.25): anima-tree
      parity (~0.28) around 100 devotion, 0.45 total at a 2000-devotion god
- [ ] Ideology integration (Evelyn, 2026-07-02): a deep-worship religion —
      meme/precepts (corpse offerings as ritual, venerated "the deep"),
      idol as styleable/ideogram building, maybe a Deepcaller-themed ritual
      that grants devotion or psyfocus. Requires Ideology (she owns all DLC)
- [x] Grasp v1: level-1 psycast, XML-only ability (base VEF ability +
      AbilityExtension_Hediff auto-applies the hediff; durationTime feeds the
      Disappears comp). Deepcaller_Grasped hediff (custom class): leash, not
      down (Evelyn, 2026-07-02: downing tanks raid morale → survivors flee on
      waking, unsatisfying). Moving ×0.4 for 15s + every 60 ticks, victims
      beyond 1.9 cells of the eruption point are dragged 2 cells back along
      the sight line (teleport + job interrupt; drag ~2 c/s beats slowed walk
      ~1.8 c/s, so they struggle but never escape). Any damage taken reduces
      severity (0.03/damage ≈ 33 to tear free), so your own tentacles pounding
      the held victim also free it. Balance knobs (GraspExtension):
      severityPerDamage, pullIntervalTicks, leashRadius, pullDistance;
      durationTime, psyfocusCost 0.05 / entropy 12. The pull logic is prior
      art for Riptide. Devotion scaling (Evelyn, 2026-07-02: base grip fine
      early, underwhelming endgame): Ability_Grasp reads HighestDevotionLevel
      at cast — duration ×(1+0.35×devotion) = 15/20/26/31s, gripStrength
      1+0.5×devotion divides break-on-damage ≈ 33/50/66/83 damage to tear
      free at devotion 0-3 (knobs in GraspExtension)
- [x] Tentacle ranged survivability (Evelyn, 2026-07-02): growth currently
      grants only HP scale (0.8/1.3/1.8/2.5 × base 1.6) — no armor beyond
      flat 0.2 sharp, no mitigation, and bigger bodySize means enemies hit
      them MORE easily. Wanted: something shield-belt-ish so they survive
      ranged focus. Options: (a) energy shield — vanilla CompShield on the
      race if it works off-apparel, or VPE Protector Overshield-style hediff,
      capacity scaling with stage; ranged-only + EMP-pop counterplay;
      (b) per-stage armor hediff granted by CompTentacleGrowth;
      (c) XML-only IncomingDamageFactor statFactors per life stage (Robust
      gene pattern) — cheapest, but invisible and helps melee too
- [x] Riptide v2 (Evelyn's devotion rework, 2026-07-02): level-2 psycast
      (prereq Raise Idol — NOT Grasp: Riptide needs devotion ≥ 10 to cast,
      so gating it behind the idol keeps the point from being a trap;
      beware VPE prerequisites are OR/Any, so single entry only). Cast applies Hediff_Riptiden ("dragged under") to every
      hostile in radius: steps 1 cell toward the center every 5 ticks
      (visible drag, ~12 c/s, knob ticksPerCell) until reaching the 1.4 ring,
      then 120-tick stun. Collisions: dragged into a wall/impassable = blunt
      damage to victim AND edifice (incl. player walls — wallDamageFactor 0
      to spare masonry); landing on an occupied ring cell bruises both pawns.
      Devotion economy: REQUIRES idol devotion ≥ 10 (gizmo greys out with
      reason — no idol, no Riptide, incl. caravans); cast range +1 per 5
      devotion (cap +8). AoE radius = SimpleCurve over devotion (Evelyn's
      spec, 2026-07-02: nerf early / endless diminishing growth):
      (10,2) (80,8) (240,12) (2160,16), interpolated, clamped outside —
      reshape in XML, no C#. Pawn collision damage 3 + 0.4×devotion; walls
      scale separately at 1×devotion (200 devotion one-shots a 200 hp
      wooden wall). Re-casting
      on a mid-drag victim redirects its current — enabled by cooldownTime 0
      (VEF cooldownTime is TICKS; entropy/psyfocus are the real limiters,
      and any cooldown outlives the ~1.3s drag). requireLineOfSight false:
      the current flows underground — cast behind walls, drag enemies into
      them. Collision damage is QUEUED via DeepcallerGameComponent and
      applied next tick: killing from inside the victim's hediff tick
      silently lost the corpse (bison/table playtest) — never TakeDamage
      from a hediff's own TickInterval. Dev gizmos on the idol: Devotion
      +5 / +100 / 0. Failsafe Disappears(600) on the hediff. Playtest-passed
      2026-07-02 (drag feel, gate, curve, redirects, wall slams, corpses).
      Earlier retunes: castTime 30 (warmup drift), full drag to ring (partial
      pulls read as nothing on fast movers)
- [x] Ink Veil v1: level-2 psycast (prereq Lash, order 3), blooms vanilla
      BlindSmoke gas over radius 4.9 — shots through it lose accuracy,
      turrets can't lock; dissipates like smokepop smoke. No line of sight
      (rises from below), range 24.9, psyfocus 0.1 / entropy 15. Doubles as
      the tactical stopgap for tentacle ranged survivability. Note: gas grid
      is hardcoded to 4 vanilla GasTypes — a custom black ink gas would need
      its own overlay system; grey smoke is the placeholder. Devotion
      scaling (Evelyn, 2026-07-02): smoke miss factor is a hardcoded flat
      ×0.7 per gas type (ShotReport) — can't deepen selectively — so
      scaling comes via (a) radius +1 per 5 devotion, cap +10 (linear
      model), and (b) Deepcaller_Inked hediff on hostiles in the bloom:
      Sight ×0.7/×0.4/×0.15 at severity 0.25/0.45/0.75, severity = 0.25 +
      0.01×devotion, decays 8/day (blinder = longer). Sight also degrades
      melee hit/dodge, and enemies-only — colonists just eat the flat smoke
      factor. Knobs in AbilityExtension_InkVeil. Playtest-passed 2026-07-02
- [x] Corpse contest (Evelyn, 2026-07-02): the Idol wins — non-mech corpses
      inside a friendly idol's radius are claimed offerings, excluded from
      tentacle auto-consumption (CompIdolDevotion.ClaimsCorpse). Plus a
      shared player toggle "Tentacles eat corpses" (gizmo on tentacles,
      scribed on DeepcallerGameComponent). Open question for the Consume
      ability: should casting Consume on an offering feed it to the idol
      (caster as priest) — or is that an undesirable nerf? Evelyn undecided
- [x] Ink Veil vs ranged summons (built 2026-07-03, once the Leviathan made
      it real): Harmony postfix on ShotReport.HitReportFor resets the
      hardcoded ×0.7 BlindSmoke factor to 1 when the shooter's race carries
      TentacleRaceExtension (tentacles + Leviathan head). Colonists still
      suffer — the veil belongs to the deep. Melee was already immune
- [ ] Ink Veil v2 (Evelyn liked it, 2026-07-02): lingering cloud entity —
      hostiles who ENTER the ink get inked too, not just cast-moment. Needs
      a ticking area thing (spawned Thing with comp, or MapComponent
      tracking active blooms) applying/refreshing Deepcaller_Inked while
      inside. Build if v1 playtests show offensive cast fully eclipsing the
      defensive blanket
- [x] Psyfocus endurance scaling (Evelyn, 2026-07-02: modded raid bosses —
      Isekai etc., thousands of HP — outlast a %-cost psycaster; psyfocus
      pool never grows with level): PathCostScalingExtension on
      Deepcaller_Path — SimpleCurve psyfocus cost factor by psylink level,
      (1,1.25) (10,0.6) (20,0.3) (30,0.1). Harmony postfix on VPE
      AbilityExtension_Psycast.GetPsyfocusUsedByPawn (single choke point:
      gating, gizmo display, deduction). Generic — any path def can carry
      the extension. Required referencing VanillaPsycastsExpanded.dll and
      bumping the csproj to net48 (VPE builds against 4.8)
- [x] Consume v1 (Evelyn's design, 2026-07-02): level-3 psycast (prereq
      Raise Idol), replaces the old "eat corpses faster" concept. Cast on
      ANY living fleshy creature (colonists/prisoners included — the deep
      does not judge): instant death, NO corpse, devotion fed remotely to
      the highest-devotion idol (bodySize × 1.5 live-offering factor —
      corpses stay the haulers'/tentacles' domain). The victim's REMAINING
      part HP × 0.2 becomes caster neural heat (wound big prey first for a
      discount) — heat capacity growing with psylink level = bigger safe
      prey with mastery. On overflow: cast still fires, then the idol eats
      the caster too — stripped (gear drops), killed, corpse consumed for
      devotion, message "reached too deep. The deep reached back."
      Requires an idol on the map (gizmo reason: "the deep has no mouth
      here"). No LoS. Knobs: heatPerHitPoint, liveOfferingFactor.
      psyfocus 0.05 / entropyGain 0 (heat IS the cost)
- [x] Deep Hoard passive (Evelyn, 2026-07-02): level-4 node (prereq
      Consume; "increased buy cost" = deeper level, since VPE hardcodes
      1 point/node — checked ITab_Pawn_Psycasts). Passive pattern copied
      from VPE-Ragnarok (Evelyn's pointer): ability class with
      ShowGizmoOnPawn=false, "Passive:" label convention, effect keyed to
      the node being learned. With it, Consume victims' equipment/apparel/
      inventory survive into the idol's belly (CompIdolDevotion is an
      IThingHolder now; hoard scribed; drops all on idol destruction).
      "Bargain with the deep" gizmo: float menu of hoarded items at market
      value × hoardGreedFactor 1.25, paid in silver lying within the
      idol's shadow (consumed on purchase). Hoard + ransom value shown in
      idol inspect string. Without the passive, gear digests as before —
      sacrifice-or-salvage stays a real choice until the node is bought.
      Pricing v2 (Evelyn, 2026-07-02): factor = SimpleCurve over devotion,
      (0,4) (100,2.5) (500,1.6) (1000,1.0 fair market) (2000,0.7), PLUS a
      hunger surcharge +0.25/day unfed beyond 3 days (living sacrifices
      count as meals — OfferRemote resets the clock; ticksSinceConsume
      doubles as the meal clock since it only resets on eating). UI is
      cinematic, not numeric (Evelyn): inspect shows "The God of the Deep
      is disdainful/dismissive/bored/curious/pleased/proud, and
      hungry/sated" (mood tiers at devotion 100/500/1000/1500/2000, knob
      moodThresholds) — exact prices still visible per-item in the bargain
      menu, so no decision info is lost. Knobs:
      hoardPriceFactorByDevotion, hungerGraceDays, hungerPenaltyPerDay.
      Bargain UI v2 (Evelyn, 2026-07-02): vanilla Dialog_Trade instead of
      float menu (scales to hundreds of items — search/sort for free).
      CompIdolDevotion implements ITrader: Goods = hoard, colony side =
      silver within the idol radius only, TraderKindDef
      Deepcaller_DeepTrader (commonality 0, permissive StockGenerator so
      WillTrade accepts anything). Harmony prefix on Tradeable.GetPriceFor
      forces price = market value × PriceFactor exactly — no negotiator
      skill/faction math; silver stays 1:1. Trader name: "The God of the
      Deep". Payment silver destroyed (swallowed); purchases drop beside
      the idol
- [x] Overgrowth v1: level-4 psycast (prereq Lash, order 2), self-cast —
      every friendly tentacle on the map advances one growth stage
      (GrantStage handles the feed math + stage-up regeneration). Greyed
      out when no tentacles are spawned ("no tentacles answer the call").
      psyfocus 0.25 / entropy 30, castTime 90. Playtest-passed 2026-07-02
- [x] Symbiote v1 (Evelyn's design, 2026-07-02): level-4 self-cast psycast
      (prereq Consume, order 3). Grafts a tentacle onto the caster:
      Hediff_Symbiote holds per-graft {maxBonus, currentBonus} lists and a
      DYNAMIC CurStage (rebuilt + Notify_HediffChanged when values move) —
      total Manipulation offset = sum of grafts. Max per graft =
      devotion/100 at cast, capped +25 (1000→+10, 2500→+25); fresh grafts
      start at −10 and adapt +2/day (~17.5 days to tame +25; knob
      adaptPerDay on hediff ext). Escalating hubris: fail chance by grafts
      already carried 0/5/10/20/40/80% (list knob, last entry holds
      beyond) — on failure the graft devours its host (strip → shared
      DeepDevourUtility: kill, no corpse, devotion offering). Requires an
      idol on map. Manipulation feeds melee hit (+12/point) and work
      speed, so this is the caster-transformation node. psyfocus 0.2 /
      entropy 25, castTime 120. Playtest-passed 2026-07-02 (RIP Diana,
      devoured on graft 5 — gear dropped correctly)
- [x] Leviathan's Wake v1 (Evelyn's octopus, 2026-07-02): level-5 capstone
      (prereq Overgrowth). Spawns Deepcaller_LeviathanHead at target +
      8 Deepcaller_Tentacle arms (born Crushers) in a fixed ring (radius
      3). ONE shared pool: CompLeviathanCore on the head, 1500 +
      2×devotion; every part (head included, linked to itself) has
      CompLeviathanPart whose PostPreApplyDamage absorbs ALL damage into
      the pool — no Harmony needed, parts can't be downed or focus-killed;
      pool empty → Collapse() kills every part at once (corpses drop →
      offerings). Part comp is on the base tentacle race but inert until
      an ability sets its core. Arms tethered: dragged back within 4.5 of
      the head (DeepPullUtility). Head: MoveSpeed 0.15, melee maw,
      CompTurretGun (War Queen pattern, no render node) firing
      Deepcaller_SludgeSpitter → SludgeGlob projectile, damage
      Deepcaller_Sludge = blunt 9 + ToxicBuildup 0.02/damage. Requires an
      idol; no LoS; 30000-tick lifespan like tentacles (head death orphans
      arms → they die). psyfocus 0.85 / entropy 60, castTime 300. Pool
      shown in inspect ("Deep vitality"). Emergent: arms still have growth
      comps — the leviathan's arms evolve as they kill
- [x] Tideguard passive (Evelyn, 2026-07-02): level-5 node (prereq
      Leviathan's Wake) — summoned tentacles get a scaling shield belt;
      the head NEVER gets one (no shield comp on its race, hediff never
      applied). Implementation: vanilla CompShield subclass
      (CompShield_Deep) on the tentacle race, inert while
      EnergyShieldEnergyMax ≤ 0 (bare CompShield at 0 max still eats one
      hit per reset cycle — gated in PostPreApplyDamage/gizmo/draw);
      Deepcaller_TideguardShield hediff grants the stats by stage
      (severity = stage+1, synced by CompTentacleGrowth on stage-up):
      max/recharge 0.4/0.06 → 0.7/0.09 → 1.1/0.13 (belt parity at
      Crusher) → 1.6/0.18. Applied at summon (Lash + Leviathan arms) only
      if the caster has learned the node. This closes the old "tentacle
      ranged survivability" backlog item — Ink Veil is the tactical
      answer, Tideguard the endgame one
- [ ] Icons/art pass (placeholders until then)
