# Progression checks

Run with the .NET 10 SDK:

```sh
dotnet run --project Tests/Progression/Progression.csproj
```

After the initial framework restore, add `--no-restore` to run offline.
There are no test-framework packages. The executable links the production
economy, stat profile, firing clock, feeding-budget, migration, Riptide force
and physical-material helpers.
Assertions cover real quotes, extreme discounts, integer overflow rejection,
affordability goals, fractional cadence, rank usefulness past visual limits,
earned-stage preservation, bounded growth/lifetime feeding, mining pressure,
body-size resistance, extreme devotion values, construct compatibility,
Bud unlock policy, ability target-picker flags and internal def references.

## 1.2.0 physical combat playtest

Restart RimWorld after installing the new local build. Use a separate test
save with a hostile mech, a hostile VPE stone/steel construct, and a hostile
RimWorld of Magic golem if that mod is enabled.

1. Cast on an empty mountain: no damage, at any devotion. Then put a chunk,
   animal or vehicle in the area with rock between it and the center. Only
   actual collision cells should be excavated. The projectile takes recoil
   even if it breaks the rock, and must survive to continue. Test ore yields
   and ordinary roof collapse. A 70 kg edge-starting projectile after three
   clear cells has roughly 8,820 raw mining damage at D=8,000, with substantial recoil.
2. Compare identical bodies at the exact center, near it and at the outer
   edge. The center occupant should turn around without injury or stun; the
   edge starts with the strongest pull. Compare 0/3/7 traveled cells, a slow
   armored nudge, opposing motion, and bodies moving together. Both sides
   should take appropriately scaled collision damage, with normal armor.
   Speed must carry through the center. Before crossing, intact rock blocks
   passage while acceleration continues and collisions cost HP. After crossing,
   air gently slows flight and impacts strongly reduce speed. Loose survivors
   receive momentum instead of acting like walls.
3. Test all eight combinations of the three favors while sated, then hungry.
   Walls/doors, creatures and possessions each require their own purchase.
   Furniture/turrets do not inherit wall protection. A protected colonist or
   vehicle hit by another projectile must still take damage. A home-area chunk
   still flies; gold accepted in storage stays grounded only with its favor.
4. Test an owned Vehicle Framework vehicle: people protection does not spare
   it; possessions protection does. Compare low/high devotion and cargo mass.
   Check full footprint collisions, turning, pathing after landing and wreckage.
   New corpses, mined chunks and dropped loot must stay outside that cast,
   including items that would otherwise merge into an existing stack.
5. Buy acceleration and compare actual acceleration and collision speed.
   Area and distance purchases must be unavailable below 5,000 devotion. At
   5,000 they cost 100/60 gold and add two radius/four cast cells independently.
   The next ranks cost 160/96. Test enlarged targeting rings past the normal
   radial limit. Widen AoE without cast distance and check caster exposure.
6. Recast during a drag, cast near map boundaries, and save/reload during a
   populated surge after casualties. Check no duplicate drivers, stale object
   references, repeated impacts or stuck pawns. Re-check favors after hunger
   changes or removing the idol while a cast is still active.
7. Cast Grasp and Ink Veil directly on each non-organic enemy. Grasp should
   slow/leash it; ink should apply the sight penalty. Riptide should pull/stun
   it. Check an enemy with zero psychic sensitivity as well.
8. Before buying the unlock, Buds should attack organic hostiles and ignore
   the machines/constructs. Buy "Buds shoot non-organics" in the idol shop
   (250 base gold; 8 gold at 8,000 devotion). Existing and newly summoned Buds
   should now shoot and damage them, including piercing hits at damage rank 5.
   The purchase must show Unlocked and cannot charge gold a second time.
9. Save/reload, then move/re-raise the idol on another map. The Bud unlock
   and previous cultivation ranks must persist for the entire brood.
10. Watch Grasper pulls, Crusher cleaves and Colossus slams against mechs and
   golems. Their corpses must remain inedible to tentacles, Consume and the
   idol; ordinary flesh remains edible. Confirm Leviathan formation still
   moves normally and scan the log for new Deepcaller errors.

## 1.1.0 progression regression checks

Before publishing, check these behaviors in a separate RimWorld test save:

1. Place gold partly inside and partly outside the idol's shadow. Buy a rank
   and verify only gold inside the radius is spent. Devotion remains intact.
2. Buy each Bud milestone at rank 5 and compare the preview with selected Bud
   stats and combat: piercing, paired volleys, and spectral trails. Check
   fire rate above rank 16 at 2,500 devotion and speed beyond the flight cap.
3. Load an older save containing a Colossus at 8 feed and old purchased Bud
   ranks. Confirm forms and ranks are retained, then save/reload again.
4. Re-raise the idol on another map, abandon its map, and restore it. Purchased
   bonuses must stay active on ordinary summons left on other maps.
5. Damage a Colossus, let it eat, and check healing and active shield recovery.
   A fully healthy/charged Colossus with ample life must leave corpses alone.
   EMP reset must remain a reset. Repeated meals cannot extend life forever.
6. Watch Grasper drag, Crusher cleave, and Colossus telegraph/slam. Damage dealt
   must award growth to contributors; healed enemies cannot refill XP budgets.
7. Buy movement cultivation with an ordinary tentacle and a Leviathan summoned.
   The tentacle's speed must change; head/arm/flower speeds and formation must not.
8. Cross an idol awakening with a corpse stockpile, then 4,000 and 8,000 devotion.
   Check title, rings, sound, digestion, pinned goal and offering receipt.

These automated checks do not replace a live playtest of engine behavior or UI.

## Isolated engine checks

`Tests/RuntimeMod` is a development-only mod, excluded from Workshop staging.
Build it with `dotnet build Tests/RuntimeMod/RuntimeChecks.csproj`, refresh
staging, then run `bash Tests/RuntimeMod/run.sh` with Steam and graphics access.
It generates a disposable map under a fresh `/tmp/deepcaller-runtime.*` save
folder, checks actual pickup/collision/purchase behavior and optional Vehicle
Framework integration, writes `DEEPCALLER_RUNTIME` results to that folder's
`Player.log`, and exits. It does not load or write a normal user save or modify
the user's active-mod list. It registers a test-mod symlink in the game's Mods
directory, but only the isolated configuration activates it.

The expanded suite passed 133 engine assertions, including real Consume casts,
hoard transactions, mechs, VPE constructs, collisions, momentum and actual
game save/reload. Log: `/tmp/deepcaller-runtime.wxP3Kc/Player.log`.
The standalone suite passes 7,836 formula/config assertions.

`bash Tests/RuntimeMod/run.sh --normal-mods` copies the user's active mod list
into the disposable profile. It passed 30 compatibility assertions with
RimWorld of Magic golems, Isekai's actual level component, vehicles, and
save/reload of momentum and new purchase ranks. Log:
`/tmp/deepcaller-runtime.vZKYpA/Player.log` (2026-09-13). It uses default
settings for the copied mod list; it does not load the user's campaign.
Use the final log marker, not just the process exit code, to judge success.
RimWorld's graphics-free mode cannot initialize its texture atlases, so this
runner uses batch mode with graphics. It does not judge visual polish or
long-term campaign feel. The full-mod run logs the same FMOD audio-format
warnings present in the user's existing Player.log.
Vehicle Framework logged a worker ThreadAbortException after the success
marker while the test process shut down; no engine assertion failed.

## Consume and hoard playtest

1. Try single-target Consume with little Devotion and a novice caster on a
   healthy, equipped enemy. Excess heat should devour both victim and caster.
2. At 2,000 Devotion, buy one radius tile for 40 gold; the next costs 64.
   Each purchase widens every cast by exactly one tile. Check nearby allies
   are included, but mechs, golems and targets outside the radius are excluded.
3. At 5,000 Devotion with five burn ranks, compare ordinary five-tile feasts
   with boss targets and already accumulated caster heat. Try ten tiles only
   in a disposable test. No radius is guaranteed safe.
4. Open Cultivate the hoard. Purchase bargaining and compare actual silver
   ransom while sated and hungry. Feed the idol past 2,000 and check the
   continuing discount, including the one-silver item minimum.
5. Buy salvage, offer a clothed corpse, and ransom its surviving gear. With
   restoration and cleansing purchased, recovered gear should be repaired
   and corpse taint removed. Quality and biocoding should stay intact.
6. Save/reload and re-raise the idol; ranks and pinned goals must persist.
