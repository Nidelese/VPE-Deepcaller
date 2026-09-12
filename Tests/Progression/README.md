# Progression checks

Run with the .NET 10 SDK:

```sh
dotnet run --project Tests/Progression/Progression.csproj
```

After the initial framework restore, add `--no-restore` to run offline.
There are no test-framework packages. The executable links the production
economy, stat profile, firing clock, feeding-budget and migration helpers.
Assertions cover real quotes, extreme discounts, integer overflow rejection,
affordability goals, fractional cadence, rank usefulness past visual limits,
earned-stage preservation, and bounded growth/lifetime feeding.

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
