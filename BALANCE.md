# Cultivation balance — 1.2.0

These are explicit playtest targets, not a claim that unlike abilities have
one objectively correct price. All purchases affect the player's brood or
casters permanently, across maps. Feeding retains its value through the
uncapped divisor even after an idol reaches its last named awakening.

`gold = max(1, ceil(baseGold × 1.6^currentRank / (1 + devotion/250)))`

Devotion is not spent. Each line compounds independently. The calculation
uses logarithms so a huge numerator cannot overflow into a cheap purchase.
One gold is the currency floor; the underlying discount continues growing.
Core gold has a market value of 10 silver per unit; actual trade quotes vary.

| Upgrade | Base gold | First purchase at 2,000 D | At 5,000 D | At 8,000 D |
|---|---:|---:|---:|---:|
| Damage | 20 | 3 | 1 | 1 |
| Fire rate | 25 | 3 | 2 | 1 |
| Bolt speed | 15 | 2 | 1 | 1 |
| Feeding | 20 | 3 | 1 | 1 |
| Regeneration | 20 | 3 | 1 | 1 |
| Mature power | 30 | 4 | 2 | 1 |
| Ordinary melee movement | 25 | 3 | 2 | 1 |
| Buds shoot non-organics¹ | 250 | 28 | 12 | 8 |
| Spare our walls¹ | 1,260 | 140 | 60 | 39 |
| Spare our people¹ | 1,680 | 187 | 80 | 51 |
| Spare our possessions¹ | 840 | 94 | 40 | 26 |
| Riptide acceleration | 40 | 5 | 2 | 2 |
| Riptide pull radius² | 2,100 | Locked | 100 | 64 |
| Riptide cast distance² | 1,260 | Locked | 60 | 39 |
| Consume burn reduction | 180 | 20 | 9 | 6 |
| Consume radius, one tile³ | 360 | 40 | 18 | 11 |
| Hoard bargaining | 180 | 20 | 9 | 6 |
| Hoard corpse salvage¹ | 900 | 100 | 43 | 28 |
| Hoard restoration | 270 | 30 | 13 | 9 |
| Hoard cleansing¹ | 630 | 70 | 30 | 20 |

¹ One purchase only. The three safety favors additionally require a sated
idol on the current map; the Bud unlock does not. Existing purchases retain
their ranks and are not charged again after this price change.

² Requires 5,000 devotion to purchase. Area and distance have independent ranks,
prices and effects. Their first prices are 1,000 and 600 silver of gold at
that threshold. A second purchase at unchanged devotion costs 160 and 96 gold.

³ Requires 2,000 Devotion. Buying a tile permanently enlarges every Consume;
there is no free radius slider. Five radius ranks cost 274 gold at D=5,000;
five burn-reduction ranks cost 138. Devotion growth lowers later quotes.

## Why these relative prices

- Existing swarm prices stay intact after review: damage is the 20-gold
  reference; fire rate costs more because its milestones add echo volleys;
  bolt speed costs less because earlier impact is less valuable than raw DPS.
  Feeding and regeneration improve survival and development, while mature
  power and movement improve the usefulness of several special attacks.
- Acceleration costs twice the reference damage rank. It improves both pull
  speed and collision energy by increasing actual acceleration. Each rank adds
  10% of baseline, with another 10% every fifth
  rank. This is additive rank scaling, not an exponentially growing stat.
- Permanent safety is priced as a strategic purchase. At 5,000 devotion the
  targets are 80 gold for keeping friendly creatures grounded, 60 for walls,
  and 40 for possessions. Multiplying each target by the divisor 21 gives the
  base prices above. These were raised from the provisional 250-gold base,
  which made them only 12 gold at this stage.
- Wider area exposes more bodies and provides longer run-up. It costs more
  than cast distance, which primarily improves placement and caster safety.
  At 5,000 devotion, the first area rank adds two radius cells, roughly 23%
  more circular area around the current 18.4-cell radius. Distance adds four
  cast cells. Wider area without longer casting reach deliberately increases
  the value of buying the people favor.

## Purchased range limits

Each area rank adds `4 × D/(D+5000)` radius cells; each distance rank adds
`8 × D/(D+5000)` cast cells. Their eventual limits therefore rise by four and
eight cells per purchase, with no fixed rank cap. This changes the limit
itself rather than just approaching the same limit more quickly.

Actual reach is bounded by the map diagonal. Beyond full-map coverage, larger
range has no tactical value; the shop disables additional purchases on that
map. Acceleration and devotion discounts remain further progression routes.
The footprint uses a cast-time thing snapshot, and large targeting outlines
avoid the engine's fixed radial lookup table.

## Physical calibration

For an unobstructed equal-length run below integration limits, +10%
acceleration gives +10% collision energy and about 4.7% less time to reach the
center. It applies to every captured object and every intervening collision,
which supports a higher base price than a single brood damage stat. An area
purchase increases target count and potential run-up; a cast-distance purchase
changes placement and caster exposure. They cannot have one universal
gold-to-damage exchange rate because terrain, mass, density and armor differ.

At 8,000 Devotion, an edge-starting 70 kg projectile with three clear cells
has about 8,820 raw mining damage and 3,530 recoil; it can break plasteel but
an ordinary human will usually pay for that impact with its life. Actual
engine checks cover surviving chunk mining, vehicle footprint damage, and
mass-weighted shoving. No empty-area mining occurs.

Acceleration ends at center; speed does not. Air drag only begins after
crossing, and collisions then dissipate energy. Before crossing, the god
replenishes momentum losses, but surviving rock still blocks passage and
recoil still consumes projectile HP. The sated wall favor explicitly stops
projectiles against protected structures. Runtime limits are eight cells per
tick with 32 swept substeps and one billion damage. Acceleration purchases
stop if even the heaviest current body at the weakest nonzero placement
cannot benefit. Movement cultivation stops at 60 cells/second; map-sized
range purchases and numerically indistinguishable ranks also stop charging.

## Consume calibration

`raw = (20 + 0.4*remainingHP + 0.04*pawnAndGearValue + 0.5*combatPower)`
`      * max(1, meleeDamageFactor) * (1 + ((IsekaiLevel-1)/50)^2)`

Each victim contributes `raw * (1 + radius²/25) / (1+(D/800)²) / burnFactor`.
The complete bill is frozen before anyone grants devotion. Burn factor is
`1 + 0.2*rank + 0.1*floor(rank/5)`. Summed heat exceeding the caster's remaining
capacity devours the caster too. A small positive numerical floor is retained.
The ordinary reference has 350 remaining HP, 1,750 silver total worth,
100 combat power and no extraordinary damage/level multiplier: 280 raw heat.

| Reference cast, five burn ranks | Total heat |
|---|---:|
| 3 ordinary victims, D=2,000, radius 1 | 57.4 |
| 6 ordinary victims, D=2,000, radius 1 | 114.8 |
| 12 ordinary victims, D=5,000, radius 5 | 79.9 |
| 1 level-1,600 equivalent boss, D=5,000, radius 5 | over 6,800 |

These are fixtures, not a guarantee for every five-tile cast. Wider areas
can contain more victims; wounds, expensive gear, buffs, bosses and existing
caster heat change the outcome. A ten-tile radius has five times the strain
of single-target Consume before counting its larger victim population.
Ungodly Devotion can eventually support it without a hard safety unlock.

## Hoard calibration

The old ransom curve stays unchanged through 2,000 D (70% market value).
Beyond that, divide it by `D/2000`, then by `1+0.2*bargainingRank`. Hunger
still adds its surcharge before those divisors. Buyback has a one-silver
minimum per item, and purchases cost gold rather than consuming Devotion.

A first bargaining rank pays for itself after recovering roughly 1,700–2,100
silver of market-value loot at 2,000–8,000 D. At 5,000 D it costs 9 gold
(90 silver), reduces the quote from 28% to 23.33%, and breaks even around
1,930 silver of recovered loot. The repeated rank price compounds, giving
players a reason to feed the idol before buying another economic advantage.

Salvage opens an additional source of stock: corpse gear normally destroyed
by idol digestion. It costs 43 gold at D=5,000. Restoration (13 gold first
rank) returns 20% of missing durability, then 33.33%, 42.86%, and so on.
Cleansing costs 30 gold and removes corpse taint on recovery. Neither changes
quality or removes biocoding. These services apply after ransom, preserving
the discount of buying worn goods. Real engine transactions test the result.

Further campaign feedback should compare casualties, collateral, time to win
and gold remaining under equal budgets. The prices now have explicit effect
and payback anchors; satisfying long-term feel still requires player feedback.
