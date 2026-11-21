# Abyssal meditation access — 2026-09-13

Unreleased follow-up to 1.2.0, responding to tanyfilina's Workshop feedback.

## Implemented

- Automatic access is **off by default**, also for existing installations
  without a Deepcaller settings file. This follows the author's preference
  that additional power should be an explicit choice in a survival game.
- Each pawn can spend **one psycast point** on the abyssal focus icon's unlock
  button in the Psycasts tab. This uses VPE's existing purchase and save system.
  No extra DLC or affinity mod is needed beyond Deepcaller's dependencies.
- Unlocking the Deepcaller path alone does not include the meditation focus.
- Options → Mod settings → Deepcaller can restore automatic access. Changes
  apply immediately, including to pawns already checked by the game. Paid
  unlocks remain when the setting is turned off. Other focus types are unaffected.
- A pawn's former free access is not grandfathered into an earned unlock.
  Existing colonies can buy the focus, accept an epiphany, or explicitly
  enable automatic access.

The XML uses VPE's `MeditationFocusExtension.pointsOnly` and keeps
`canBeUnlocked=true`. The optional setting is applied after the public
availability-cache query, so the setting does not contaminate cached results
or require a map/world-wide cache purge. VPE owns earned-unlock invalidation.

## Epiphanies

All chances apply to free player colonists who have not learned the focus.
A success offers a persistent choice letter: accept free abyssal meditation,
postpone the choice, or refuse further invitations for that pawn. Refusal
leaves the point purchase available. Acceptance does not grant a psylink,
points or a psycast path; it persists even if the pawn gains a psylink later.

| Trigger | Chance and frequency |
|---|---|
| Sleeping with Symbiote grafts | Independent 5% per attached graft per sleep cycle; two grafts = 9.75%. Summoned tentacles standing on the map do not count. |
| A summoned tentacle deals hostile damage | 1% for its caller, once per tentacle per fight. Incoming or outgoing hostile damage keeps the fight active; 2,500 quiet ticks (one game hour) separates fights. Mechs count. |
| Eldritch cult or Esotericism | One 0.5% roll per sleep cycle, completed prayer, meditation, grave visit, or completed ritual with actual attendance. Having both memes does not double this chance. |
| Within 10 tiles of a tentacle finishing a corpse meal | 1% per meal per pawn, unless the pawn's food-related ideology objects to this corpse. Range includes exactly 10 tiles; no line-of-sight requirement. |

Sleep is sampled every 250 ticks for eligible pawns only. A noon-to-noon
local cycle avoids giving two rolls during a night crossing midnight and
also supports day sleepers. Interrupted reflection jobs and canceled rituals
grant no roll. The feeding check uses negative ideology-derived ingestion
thoughts and their nullifiers, covering human flesh, vegetarian and venerated
animal beliefs without treating ordinary raw/rotten food disgust as religious
opposition. It creates no eating memories for observers.

Learned, accepted, refused and pending pawns leave the candidate roster.
Recurring events do not query their focus eligibility or roll for them.
Enrollment is refreshed on spawn, faction changes, settings changes, VPE
focus purchase/reset, and game loading. Sleep and combat roll state,
summoner bindings, accepted/refused choices and pending letters are saved.
Pre-update ordinary summons have no saved caller, so combat invitations begin
with newly summoned tentacles; Leviathan parts can resolve their existing caller.

## Affinity definitions

Only Eldritch cult and Esotericism enable the reflective chance in this change.
No ocean biome, Ocean Lover trait, Merren xenotype or Pain is virtue bonus is
enabled. The god's theme is extradimensional contact.

| Suggested affinity | Exact definition | Source checked |
|---|---|---|
| Pain is virtue | MemeDef `PainIsVirtue` | Installed Ideology `Defs/MemeDefs/Memes_Misc.xml` |
| Eldritch cult | MemeDef `VME_Structure_ChthonianCult` | Installed Vanilla Ideology Expanded – Memes and Structures, 1.6 `Defs/MemeDefs/Memes_Structures_Basic.xml` |
| Esotericism | MemeDef `VME_Structure_Esotericism` | Same installed source; this is a structure meme |
| Ocean lover | TraitDef `VTE_ChildOfSea` | Installed Vanilla Traits Expanded, 1.6 `Defs/TraitDefs/Traits_Singular.xml` |
| Coastal / abyssal Merren | XenotypeDefs `Stagz_Merfolk_Coastal`, `Stagz_Merfolk_Abyssal` | [Author's current 1.6 definitions](https://github.com/Goji-2337/StagzMerfolk/blob/master/1.6/Defs/XenotypeDefs.xml); mod not installed locally |

Future integration should inspect the pawn's actual ideology, unsuppressed
trait, or current xenotype, with optional def lookups and no new hard mod
dependency. Conversion, trait suppression and xenotype changes must update
eligibility. Hybrid/custom xenotypes need an explicit design decision; matching
translated labels or granting access to every aquatic gene would be too broad.

A dedicated deep-worship meme and offering ritual remain future work; the
original Ideology TODO in DESIGN.md is still open.

## Verification

Build the runtime harness and run `bash Tests/RuntimeMod/run.sh --meditation`.
The isolated test uses real VPE unlocks, idol focus eligibility, settings
serialization, and a full game save/load. It checks the default restriction,
an ordinary tribal, immediate toggles, one-point spending, per-pawn access,
and preservation of paid access after reload.

The initial access pass passed **16 engine assertions**, including settings
serialization and game reload. Log:
`/tmp/deepcaller-runtime.QXvRL2/Player.log`; selected evidence is in
`Tests/RuntimeMod/verification-meditation.txt`. The fixture's initial random
family-reference warning was resolved by generating unrelated test pawns.
The epiphany pass builds with zero warnings/errors and passed **30 engine
assertions**, using installed cult/esoteric definitions, real VPE purchases,
damage and job hooks, choices, ideological disgust, and full game reload.
Run `bash Tests/RuntimeMod/run.sh --epiphanies`; log:
`/tmp/deepcaller-runtime.d4LD3N/Player.log`. Selected evidence is in
`Tests/RuntimeMod/verification-epiphanies.txt`.
Vehicle Framework logs worker aborts during process shutdown after success.
This change is staged locally and has not been published to Workshop.
