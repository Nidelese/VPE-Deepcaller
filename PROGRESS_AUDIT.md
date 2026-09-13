# Conversation goals — 1.2.0, 2026-09-13

The interruptions did not cancel these goals. This ledger records the result
of completing the gaps found in the previous audit.

| Requested outcome | Result |
|---|---|
| Individual tentacle growth, useful mature meals, milestone attacks and idol awakenings | Retained from the released 1.1 progression pass. |
| Permanent gold upgrades, independently compounded prices and uncapped Devotion divisor | Implemented; quotes, payment, numerical limits and pinned goals tested. |
| Ordinary melee movement, excluding Leviathan formation parts | Retained; saturated movement purchases stop charging. |
| Physical CC, Grasp, Ink and slams accept non-organics; paid Bud targeting | Implemented; actual Grasp/Ink casts tested on mechs, VPE stone constructs and RoM golems. Eating remains a separate flesh-only rule. |
| Riptide mines only through spontaneous projectile impacts | Implemented; empty mountains stay untouched, real rock breaks only on contact. |
| Creatures, cows, chunks, vehicles and loose wealth can become projectiles | Implemented with mass, full vehicle footprints and cast-time rosters. Fresh drops and wreckage remain excluded. |
| Acceleration ends at center, speed does not | Implemented with integrated velocity, center-crossing integration and coasting. Pre-center collisions cost HP while momentum is replenished; intact obstacles still block movement. Post-center air and collisions dissipate motion. |
| Collisions affect both objects; surviving loose objects receive momentum | Implemented and engine-tested. A light bar cannot halt a truck; protected people can still be injured and shoved. Armor remains effective. |
| Three independent wall/people/possessions favors, purchased and sated | Implemented; all eight purchase combinations tested hungry and sated. Wall protection is the explicit stopping exception. Unclaimed chunks remain hazardous. |
| Strongest starting pull at included outer edge; center occupant turns | Implemented and tested; exact-center occupants are not translated or injured. |
| Purchased acceleration and independent AoE/cast-distance extensions at 5,000 D | Implemented; separate ranks, gates, payment and effective-limit checks. |
| Whole-shop price and progression review | Completed with explicit effects, gold/silver values, marginal acceleration benefit, hoard payback and Consume reference casts in BALANCE.md. Campaign feel is not mathematically guaranteed. |
| Worth-based Consume heat, purchased burn reduction and one-tile AoE ranks | Implemented after Riptide momentum/save tests passed. Actual casts test novice death, late survival, allied offerings and non-organic exclusion. Installed Isekai level integration is tested. |
| New permanent upgrades specifically for the older hoard shop | Implemented: bargaining, corpse-gear salvage, restoration on recovery and corpse-taint cleansing. Actual transactions tested. Devotion discounts continue above the old final tier. |
| Game save/reload and compatibility checks | Mid-flight momentum, compressed-object references, new ranks and pinned goals tested. A disposable run with the user's active mod list passed 30 compatibility assertions. |
| Commit, push, publish and verify Workshop | Committed and pushed as 768feb6. Steam accepted the public update; all 123 downloaded files match the release by SHA-256 with no missing, changed or extra files. Evidence: Workshop/release_verification_1.2.0.json. |

## Evidence and limits

- Build succeeds with no warnings/errors; 20 release XML files parse.
- Standalone suite: 7,836 assertions. Isolated engine suite: 133 assertions, including additional limit and
  indestructible-obstacle checks.
- Riptide's 900-chunk, 60-tick stress fixture completed in roughly 0.7–1.2 s
  across runs (12–20 ms per surge tick). This is a dense, zero-damage fixture
  where all chunks survive; it is not a guarantee of whole-game frame rate.
- Main engine log: `/tmp/deepcaller-runtime.wxP3Kc/Player.log`.
- Normal mod-list compatibility log: `/tmp/deepcaller-runtime.vZKYpA/Player.log`.
  It includes actual Isekai, RoM, VPE and vehicle checks, and game save/reload.
- The normal-mod run has FMOD audio-format warnings also seen in the user's
  original Player.log. Vehicle Framework worker aborts occur on test shutdown
  after the success marker. No Deepcaller assertion fails in these runs.
- Numerical/performance bounds remain: map-sized reach, eight movement cells
  per tick, bounded damage and currency floors. Effective-limit checks avoid
  selling exhausted progression. This is practical continuing scaling, not
  literal infinite precision in the engine.
- Visual feel, a long campaign and every possible mod interaction still need
  player feedback. `Tests/Progression/README.md` contains the playtest checklist.

The WASD troubleshooting was explicitly canceled with “nevermind.” It is not
an outstanding feature. New support requests can be tracked independently.
