using System;
using System.Collections.Generic;
using System.Linq;
using Deepcaller;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace DeepcallerRuntimeChecks
{
    [StaticConstructorOnStartup]
    public static class RuntimeChecks
    {
        private static bool entered, ran;
        private static int checks;
        private static Map map;
        private static Pawn caster;
        private static CompIdolDevotion idol;
        private static readonly IntVec3 Center = new IntVec3(60, 0, 60);
        private static readonly List<Thing> fixtures = new List<Thing>();
        private static readonly HashSet<Thing> hitThings = new HashSet<Thing>();
        private static bool loading;
        private static int savedBodyId;
        private static CurrentVector savedPosition, savedVelocity;
        private static bool savedCrossed;
        private static bool restoredState;

        static RuntimeChecks()
        {
            if (!Environment.GetCommandLineArgs().Contains("-deepcaller-tests")) return;
            var harmony = new Harmony("Deepcaller.RuntimeChecks");
            harmony.Patch(AccessTools.Method(typeof(Root_Entry), "Update"), postfix:
                new HarmonyMethod(typeof(RuntimeChecks), nameof(Enter)));
            harmony.Patch(AccessTools.Method(typeof(Root_Play), "Update"), postfix:
                new HarmonyMethod(typeof(RuntimeChecks), nameof(Run)));
            harmony.Patch(AccessTools.Method(typeof(Thing_RiptideSurge), nameof(Thing_RiptideSurge.ExposeData)), postfix:
                new HarmonyMethod(typeof(RuntimeChecks), nameof(LoadedSurge)));
            harmony.Patch(AccessTools.Method(typeof(Thing), nameof(Thing.TakeDamage)), prefix:
                new HarmonyMethod(typeof(RuntimeChecks), nameof(ObserveHit)));
            Log.Message("DEEPCALLER_RUNTIME: harness loaded");
        }

        public static void Enter()
        {
            if (entered || LongEventHandler.ShouldWaitForEvent) return;
            entered = true;
            LongEventHandler.QueueLongEvent(() => { }, "Play", "GeneratingMap", true, null);
        }

        public static void ObserveHit(Thing __instance) => hitThings.Add(__instance);

        public static void LoadedSurge(Thing_RiptideSurge __instance)
        {
            if (!loading || Scribe.mode != LoadSaveMode.PostLoadInit) return;
            var body = Bodies(__instance).Single(b => b.thing.thingIDNumber == savedBodyId);
            restoredState = (body.position - savedPosition).Length < 1e-7
                && (body.velocity - savedVelocity).Length < 1e-7 && body.crossed == savedCrossed;
            Log.Message("DEEPCALLER_RUNTIME RELOAD: state restored=" + restoredState);
        }

        private static void Check(bool condition, string message)
        {
            checks++;
            if (!condition) throw new Exception(message);
            Log.Message("DEEPCALLER_RUNTIME PASS: " + message);
        }

        public static void Run()
        {
            if (ran || Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null
                || LongEventHandler.ShouldWaitForEvent) return;
            ran = true;
            try
            {
                Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                map = Find.CurrentMap;
                if (loading)
                {
                    var loaded = map.listerThings.AllThings.OfType<Thing_RiptideSurge>().Single();
                    var body = Bodies(loaded).Single(b => b.thing.thingIDNumber == savedBodyId);
                    // The game may advance its first tick before Root_Play.Update.
                    // Exact serialization is checked at PostLoadInit above.
                    Check(restoredState, "game reload preserves subcell position, velocity and center crossing");
                    Check(loaded.Captured(body.thing), "game reload restores the frozen collision roster");
                    var progress = DeepcallerGameComponent.Instance.cultivation;
                    Check(progress.Rank(BudUpgradeKind.HoardBargaining) == 3 && progress.Rank(BudUpgradeKind.ConsumeArea) == 5
                        && progress.Rank(BudUpgradeKind.ConsumeBurn) == 7 && progress.pinned == (int)BudUpgradeKind.ConsumeBurn,
                        "game reload retains new hoard and Consume ranks and the pinned goal");
                    Advance(loaded, 3);
                    Check(body.position.x < savedPosition.x && body.velocity.Length > 0, "reloaded projectile resumes coasting");
                    Log.Message("DEEPCALLER_RUNTIME SUCCESS: " + checks + " engine assertions including game reload");
                    Application.Quit(0);
                    return;
                }
                foreach (var thing in map.listerThings.AllThings.ToArray())
                    if (thing.Position.x >= 45 && thing.Position.x <= 80 && thing.Position.z >= 45 && thing.Position.z <= 80
                        && thing.def.destroyable) thing.Destroy();
                for (int x = 45; x <= 80; x++)
                for (int z = 45; z <= 80; z++)
                {
                    map.roofGrid.SetRoof(new IntVec3(x, 0, z), null);
                    map.terrainGrid.SetTerrain(new IntVec3(x, 0, z), TerrainDefOf.Soil);
                }
                caster = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
                GenSpawn.Spawn(caster, new IntVec3(46, 0, 46), map);
                var idolThing = ThingMaker.MakeThing(Deepcaller_DefOf.Deepcaller_Idol);
                idolThing.SetFaction(Faction.OfPlayer);
                GenSpawn.Spawn(idolThing, new IntVec3(48, 0, 46), map);
                idol = idolThing.TryGetComp<CompIdolDevotion>();
                AccessTools.Field(typeof(CompIdolDevotion), "devotion").SetValue(idol, 8000.0);
                if (Environment.GetCommandLineArgs().Contains("--normal-mods"))
                {
                    Compatibility();
                    PrepareReload();
                    return;
                }
                Policy();
                Collisions();
                Momentum();
                Purchases();
                Hoard();
                Consume();
                NonOrganic();
                Vehicles();
                Stress();
                PrepareReload();
            }
            catch (Exception ex)
            {
                Log.Error("DEEPCALLER_RUNTIME FAILURE: " + ex);
                Application.Quit(1);
            }
        }

        private static Thing Item(string name, int x = 70, int z = 60)
        {
            var thing = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed(name));
            GenSpawn.Spawn(thing, new IntVec3(x, 0, z), map);
            fixtures.Add(thing);
            return thing;
        }

        private static Pawn Person(int x, int z)
        {
            var pawn = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
            GenSpawn.Spawn(pawn, new IntVec3(x, 0, z), map);
            fixtures.Add(pawn);
            return pawn;
        }

        private static void Reset()
        {
            foreach (var fixture in fixtures)
                if (!fixture.Destroyed) fixture.Destroy();
            fixtures.Clear();
            foreach (var surge in map.listerThings.ThingsOfDef(DefDatabase<ThingDef>.GetNamed("Deepcaller_RiptideSurge")).ToArray())
                surge.Destroy();
            DeepcallerGameComponent.Instance.cultivation = new CultivationProgress();
            idol.Hoard.ClearAndDestroyContents();
            AccessTools.Field(typeof(CompIdolDevotion), "ticksSinceConsume").SetValue(idol, 0);
        }

        private static void Policy()
        {
            Reset();
            var person = Person(70, 65);
            var chunk = Item("ChunkGranite");
            var gold = Item("Gold", 71);
            var storage = new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile, map.zoneManager);
            storage.settings.filter.SetAllow(ThingDefOf.Gold, true);
            map.zoneManager.RegisterZone(storage);
            storage.AddCell(gold.Position);
            var wall = ThingMaker.MakeThing(ThingDefOf.Wall, ThingDefOf.WoodLog);
            wall.SetFaction(Faction.OfPlayer);
            GenSpawn.Spawn(wall, new IntVec3(69, 0, 60), map);
            fixtures.Add(wall);
            for (int combination = 0; combination < 8; combination++)
            foreach (bool hungry in new[] { false, true })
            {
                var progress = new CultivationProgress();
                DeepcallerGameComponent.Instance.cultivation = progress;
                progress.Import(BudUpgradeKind.RiptideRestraint, combination & 1);
                progress.Import(BudUpgradeKind.RiptideDiscernment, (combination >> 1) & 1);
                progress.Import(BudUpgradeKind.RiptidePossessions, (combination >> 2) & 1);
                AccessTools.Field(typeof(CompIdolDevotion), "ticksSinceConsume").SetValue(idol, hungry ? 600000 : 0);
                Check(RiptideImpactUtility.AffectsThing(person, caster) == (hungry || (combination & 2) == 0), "people favor " + combination + "/" + hungry);
                Check(RiptideImpactUtility.AffectsThing(gold, caster) == (hungry || (combination & 4) == 0), "stored gold favor " + combination + "/" + hungry);
                Check(RiptideImpactUtility.AffectsThing(chunk, caster), "unclaimed chunk flies " + combination + "/" + hungry);
                Check(RiptideImpactUtility.Resolve(chunk, wall, caster) == (!hungry && (combination & 1) != 0
                    ? RiptideImpact.Stop : RiptideImpact.Smash), "wall favor " + combination + "/" + hungry);
            }
            map.zoneManager.DeregisterZone(storage);
        }

        private static Thing_RiptideSurge Cast(List<Thing> moving, List<Thing> collision, float damage = 30, double devotion = 8000,
            int ticksPerCell = 1, float wallDamage = 8000)
        {
            Thing_RiptideSurge.Spawn(map, Center, caster, devotion,
                new AbilityExtension_Riptide { collisionDamageBase = damage, collisionDamagePerDevotion = 0,
                    wallDamageBase = wallDamage, wallDamagePerDevotion = 0, ticksPerCell = ticksPerCell },
                10, moving, collision);
            return map.listerThings.ThingsOfDef(DefDatabase<ThingDef>.GetNamed("Deepcaller_RiptideSurge"))
                .OfType<Thing_RiptideSurge>().SingleOrDefault();
        }

        private static void Advance(Thing_RiptideSurge surge, int ticks)
        {
            for (int i = 0; i < ticks && surge != null && !surge.Destroyed; i++)
                AccessTools.Method(typeof(Thing_RiptideSurge), "Tick").Invoke(surge, null);
        }

        private static List<RiptideBody> Bodies(Thing_RiptideSurge surge) =>
            (List<RiptideBody>)AccessTools.Field(typeof(Thing_RiptideSurge), "bodies").GetValue(surge);

        private static void Momentum()
        {
            Reset();
            var chunk = Item("ChunkGranite", 64);
            var rock = Item("Granite", 63);
            var cast = Cast(new List<Thing> { chunk }, new List<Thing> { chunk, rock }, 0, 80, 5, 0);
            var body = Bodies(cast).Single();
            Advance(cast, 6);
            double firstSpeed = body.velocity.Length;
            Advance(cast, 6);
            Check(!chunk.Destroyed && !rock.Destroyed && chunk.Position.x > rock.Position.x,
                "intact rock blocks a powered projectile without phasing");
            Check(!body.crossed && body.velocity.Length > firstSpeed && body.simulating,
                "before center the god replenishes collision losses and keeps accelerating");

            Reset();
            chunk = Item("ChunkGranite", 64);
            rock = Item("Granite", 57);
            cast = Cast(new List<Thing> { chunk }, new List<Thing> { chunk, rock }, 0, 80, 5, 0);
            body = Bodies(cast).Single();
            for (int i = 0; i < 100 && !body.crossed; i++) Advance(cast, 1);
            double crossingSpeed = body.velocity.Length;
            Check(body.crossed && crossingSpeed > 0.1 && chunk.Position.x <= Center.x,
                "crossing the center ends acceleration without erasing speed");
            Advance(cast, 20);
            Check(!rock.Destroyed && chunk.Position.x > rock.Position.x && body.velocity.Length < crossingSpeed * 0.3,
                "a post-center wall collision removes much more momentum than air");

            Reset();
            chunk = Item("ChunkGranite", 70);
            var gold = Item("Gold", 67);
            cast = Cast(new List<Thing> { chunk }, new List<Thing> { chunk, gold }, 0, 80, 5, 0);
            var goldBody = Bodies(cast).Single(b => b.thing == gold);
            Advance(cast, 15);
            Check(!goldBody.directTarget && gold.Position.x < 67 && chunk.Position.x < 67,
                "a light collateral object receives momentum without halting its heavier projectile");

            Reset();
            idol.Progress.Import(BudUpgradeKind.RiptideDiscernment, 1);
            var person = Person(67, 60);
            chunk = Item("ChunkGranite", 70);
            cast = Cast(new List<Thing> { chunk, person }, new List<Thing> { chunk, person }, 0, 80, 5, 0);
            var personBody = Bodies(cast).Single(b => b.thing == person);
            Advance(cast, 20);
            Check(person.Position.x < 67 && !personBody.started,
                "people favor still permits a collision to shove a colonist without divine pickup");

            Reset();
            chunk = Item("ChunkGranite", 64);
            rock = Item("Granite", 63);
            bool usesHealth = rock.def.useHitPoints;
            try
            {
                rock.def.useHitPoints = false;
                AccessTools.Field(typeof(CompIdolDevotion), "devotion").SetValue(idol, 80.0);
                caster.ChangePsylinkLevel(1, false);
                var ability = new Ability_Riptide { pawn = caster, holder = caster,
                    def = DefDatabase<VEF.Abilities.AbilityDef>.GetNamed("Deepcaller_Riptide") };
                ability.Init();
                ability.Cast(new RimWorld.Planet.GlobalTargetInfo(Center, map));
                cast = map.listerThings.AllThings.OfType<Thing_RiptideSurge>().Single();
                Advance(cast, 12);
                Check(cast.Captured(rock) && !rock.Destroyed && chunk.Position.x > rock.Position.x,
                    "actual casts collide with anchored objects even when a mod gives them no hit points");
            }
            finally { rock.def.useHitPoints = usesHealth; }
        }

        private static void Stress()
        {
            Reset();
            var chunks = new List<Thing>();
            for (int x = 46; x < 76; x++)
            for (int z = 46; z < 76; z++) chunks.Add(Item("ChunkGranite", x, z));
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var cast = Cast(chunks, chunks, 0, 8000, 5, 0);
            Advance(cast, 60);
            watch.Stop();
            Log.Message("DEEPCALLER_RUNTIME BENCHMARK: 900 chunks, 60 surge ticks: " + watch.ElapsedMilliseconds + " ms");
            Check(chunks.All(t => t.Destroyed || t.Position.InBounds(map)), "dense chunk field stays within map bounds");
        }

        private static void PrepareReload()
        {
            Reset();
            var chunk = Item("ChunkGranite", 64);
            var cast = Cast(new List<Thing> { chunk }, new List<Thing> { chunk }, 0, 80, 5, 0);
            var body = Bodies(cast).Single();
            for (int i = 0; i < 100 && !body.crossed; i++) Advance(cast, 1);
            savedBodyId = chunk.thingIDNumber;
            savedPosition = body.position;
            savedVelocity = body.velocity;
            savedCrossed = body.crossed;
            idol.Progress.Import(BudUpgradeKind.HoardBargaining, 3);
            idol.Progress.Import(BudUpgradeKind.ConsumeArea, 5);
            idol.Progress.Import(BudUpgradeKind.ConsumeBurn, 7);
            idol.Progress.pinned = (int)BudUpgradeKind.ConsumeBurn;
            GameDataSaveLoader.SaveGame("DeepcallerRuntimeMomentum");
            Check(System.IO.File.Exists(GenFilePaths.FilePathForSavedGame("DeepcallerRuntimeMomentum")), "isolated game save is written");
            loading = true;
            ran = false;
            GameDataSaveLoader.LoadGame("DeepcallerRuntimeMomentum");
        }

        private static void Collisions()
        {
            Reset();
            var emptyRock = Item("Granite", 65);
            Check(Cast(new List<Thing>(), new List<Thing> { emptyRock }) == null && emptyRock.HitPoints == emptyRock.MaxHitPoints,
                "empty mountain is untouched");

            Reset();
            var center = Person(60, 60);
            var rotation = center.Rotation;
            int hediffs = center.health.hediffSet.hediffs.Count;
            Advance(Cast(new List<Thing> { center }, new List<Thing> { center }), 5);
            Check(center.Position == Center && center.Rotation.AsInt == (rotation.AsInt + 2) % 4
                && center.health.hediffSet.hediffs.Count == hediffs, "exact center turns without throwing or injuring");

            Reset();
            DeepcallerGameComponent.Instance.cultivation.Import(BudUpgradeKind.RiptideDiscernment, 1);
            var protectedPerson = Person(69, 60);
            float before = protectedPerson.health.summaryHealth.SummaryHealthPercent;
            var chunk = Item("ChunkGranite");
            int chunkHp = chunk.HitPoints;
            Advance(Cast(new List<Thing> { chunk, protectedPerson }, new List<Thing> { chunk, protectedPerson }, 300), 5);
            Check(protectedPerson.Dead || protectedPerson.health.summaryHealth.SummaryHealthPercent < before,
                "a spared colonist is hurt by an unclaimed chunk");
            Check(chunk.Destroyed || chunk.HitPoints < chunkHp, "the chunk also takes recoil");

            Reset();
            chunk = Item("ChunkGranite");
            var cast = Cast(new List<Thing> { chunk }, new List<Thing> { chunk });
            var dropped = Item("Gold", 69);
            Advance(cast, 15);
            Check(!dropped.Destroyed && dropped.Position == new IntVec3(69, 0, 60), "newly dropped loot is neither pulled nor hit");

            Reset();
            chunk = Item("ChunkGranite");
            var rock = Item("Granite", 66);
            Advance(Cast(new List<Thing> { chunk }, new List<Thing> { chunk, rock }, 0), 15);
            Check(rock.Destroyed && chunk.Position.x < Center.x, "a surviving projectile mines rock and coasts beyond the center");

            Reset();
            var gold = Item("Gold");
            cast = Cast(new List<Thing> { gold }, new List<Thing> { gold }, 0);
            var freshGold = ThingMaker.MakeThing(ThingDefOf.Gold);
            fixtures.Add(freshGold);
            Check(!gold.TryAbsorbStack(freshGold, true) && gold.stackCount == 1 && !freshGold.Destroyed,
                "new gold cannot merge into a captured stack");
            Advance(cast, 15);
            Check(gold.TryAbsorbStack(freshGold, true) && gold.stackCount == 2, "stacking resumes after the cast");
        }

        private static void Purchases()
        {
            Reset();
            AccessTools.Field(typeof(CompIdolDevotion), "devotion").SetValue(idol, 5000.0);
            foreach (var quote in new[] {
                (BudUpgradeKind.RiptideRestraint, 60), (BudUpgradeKind.RiptideDiscernment, 80),
                (BudUpgradeKind.RiptidePossessions, 40), (BudUpgradeKind.RiptideArea, 100),
                (BudUpgradeKind.RiptideReach, 60), (BudUpgradeKind.RiptideAcceleration, 2) })
                Check(idol.TryUpgradePrice(quote.Item1, out int cost) && cost == quote.Item2, "shop quote " + quote.Item1);
            var gold = Item("Gold", 48, 48);
            gold.stackCount = 1000;
            AccessTools.Field(typeof(CompIdolDevotion), "devotion").SetValue(idol, 4999.0);
            Check(!idol.PurchaseUpgrade(BudUpgradeKind.RiptideArea) && gold.stackCount == 1000,
                "area purchase refuses 4,999 devotion without spending gold");
            AccessTools.Field(typeof(CompIdolDevotion), "devotion").SetValue(idol, 5000.0);
            Check(idol.PurchaseUpgrade(BudUpgradeKind.RiptideArea) && gold.stackCount == 900 && idol.TotalDevotion == 5000,
                "area purchase spends the quoted gold and retains devotion");
            Check(idol.Progress.Rank(BudUpgradeKind.RiptideArea) == 1 && idol.Progress.Rank(BudUpgradeKind.RiptideReach) == 0,
                "area and cast distance purchases are independent");
            Check(idol.TryUpgradePrice(BudUpgradeKind.RiptideArea, out int second) && second == 160,
                "second area rank compounds to 160 gold");
            idol.Progress.Import(BudUpgradeKind.RiptideArea, 10000);
            Check(idol.RangeCoversMap(BudUpgradeKind.RiptideArea) && !idol.PurchaseUpgrade(BudUpgradeKind.RiptideArea),
                "no charge for extending a footprint that already covers the map");
            idol.Progress.Import(BudUpgradeKind.MoveSpeed, 1000);
            Check(!idol.PurchaseUpgrade(BudUpgradeKind.MoveSpeed) && gold.stackCount == 900,
                "saturated ordinary movement cannot consume more gold");
            AccessTools.Field(typeof(CompIdolDevotion), "devotion").SetValue(idol, 1999.0);
            Check(!idol.PurchaseUpgrade(BudUpgradeKind.ConsumeArea) && gold.stackCount == 900,
                "Consume area rejects a purchase below 2,000 devotion");
            AccessTools.Field(typeof(CompIdolDevotion), "devotion").SetValue(idol, 2000.0);
            Check(idol.PurchaseUpgrade(BudUpgradeKind.ConsumeArea) && gold.stackCount == 860
                && idol.Progress.Rank(BudUpgradeKind.ConsumeArea) == 1 && idol.TotalDevotion == 2000,
                "40 gold buys exactly the first Consume tile without spending devotion");
            Check(idol.TryUpgradePrice(BudUpgradeKind.ConsumeArea, out int tilePrice) && tilePrice == 64,
                "the second Consume tile compounds independently to 64 gold");
            DeepcallerGameComponent.Instance.cultivation = new CultivationProgress();
            idol.Progress.Import(BudUpgradeKind.RiptideAcceleration, 1000);
            AccessTools.Field(typeof(CompIdolDevotion), "devotion").SetValue(idol, 1e300);
            Check(idol.UpgradeSaturated(BudUpgradeKind.RiptideAcceleration)
                && !idol.PurchaseUpgrade(BudUpgradeKind.RiptideAcceleration) && gold.stackCount == 860,
                "even affordable extreme acceleration ranks refuse payment when current-area movement is saturated");
        }

        private static void Hoard()
        {
            Reset();
            AccessTools.Field(typeof(CompIdolDevotion), "devotion").SetValue(idol, 2000.0);
            Check(Math.Abs(idol.PriceFactor - 0.7) < 1e-5, "old 2,000-devotion ransom price is preserved");
            AccessTools.Field(typeof(CompIdolDevotion), "devotion").SetValue(idol, 5000.0);
            Check(Math.Abs(idol.PriceFactor - 0.28) < 1e-5, "ransom keeps improving above the old final devotion tier");
            var gold = Item("Gold", 48, 48);
            gold.stackCount = 1000;
            Check(idol.PurchaseUpgrade(BudUpgradeKind.HoardBargaining)
                && gold.stackCount == 991 && Math.Abs(idol.PriceFactor - 0.28 / 1.2) < 1e-5,
                "permanent bargaining costs 9 gold at 5,000 and improves actual ransom");
            AccessTools.Field(typeof(CompIdolDevotion), "ticksSinceConsume").SetValue(idol, 600000);
            Check(idol.PriceFactor > 0.28, "hunger still worsens an upgraded hoard bargain");
            AccessTools.Field(typeof(CompIdolDevotion), "ticksSinceConsume").SetValue(idol, 0);
            Check(idol.PurchaseUpgrade(BudUpgradeKind.HoardSalvage) && idol.PurchaseUpgrade(BudUpgradeKind.HoardCleansing)
                && idol.PurchaseUpgrade(BudUpgradeKind.HoardRestoration), "the four hoard upgrades buy independently");
            int remaining = gold.stackCount;
            Check(!idol.PurchaseUpgrade(BudUpgradeKind.HoardSalvage) && gold.stackCount == remaining,
                "one-time salvage cannot charge twice");
            var person = Person(50, 48);
            person.apparel.DestroyAll();
            var shirt = (Apparel)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Apparel_BasicShirt"), ThingDefOf.Cloth);
            person.apparel.Wear(shirt);
            person.Kill(null);
            fixtures.Add(person.Corpse);
            shirt.HitPoints = Math.Max(1, shirt.MaxHitPoints / 2);
            int before = shirt.HitPoints;
            AccessTools.Field(typeof(CompIdolDevotion), "ticksSinceConsume").SetValue(idol, 10000);
            idol.CompTickRare();
            Check(idol.Hoard.Contains(shirt) && !shirt.Destroyed, "idol corpse feeding salvages apparel into its hoard");
            Check(shirt.WornByCorpse, "salvaged corpse apparel retains its taint until recovery");
            idol.GiveSoldThingToPlayer(shirt, 1, caster);
            Check(shirt.Spawned && !idol.Hoard.Contains(shirt) && shirt.HitPoints > before && !shirt.WornByCorpse,
                "actual recovery repairs purchased gear and removes its corpse taint");
        }

        private static Ability_Consume ConsumeAbility(Pawn officiant)
        {
            officiant.ChangePsylinkLevel(1, false);
            var ability = new Ability_Consume { pawn = officiant, holder = officiant,
                def = DefDatabase<VEF.Abilities.AbilityDef>.GetNamed("Deepcaller_Consume") };
            ability.Init();
            return ability;
        }

        private static void Consume()
        {
            Reset();
            AccessTools.Field(typeof(CompIdolDevotion), "devotion").SetValue(idol, 0.0);
            var officiant = Person(52, 60);
            var victim = Person(58, 60);
            var ability = ConsumeAbility(officiant);
            Check(officiant.psychicEntropy.WouldOverflowEntropy((float)ability.HeatFor(victim, 0, 0, 0)),
                "an actual novice cannot safely disappear a healthy equipped human");
            ability.Cast(new RimWorld.Planet.GlobalTargetInfo(victim));
            Check(officiant.Dead && victim.Dead && victim.Corpse == null,
                "overpriced Consume actually devours both the offering and its novice caster");

            Reset();
            AccessTools.Field(typeof(CompIdolDevotion), "devotion").SetValue(idol, 5000.0);
            idol.Progress.Import(BudUpgradeKind.ConsumeBurn, 5);
            idol.Progress.Import(BudUpgradeKind.ConsumeArea, 5);
            officiant = Person(46, 60);
            ability = ConsumeAbility(officiant);
            var first = Person(60, 60);
            var ally = Person(61, 60);
            var outside = Person(66, 60);
            var mech = PawnGenerator.GeneratePawn(PawnKindDefOf.Mech_Scyther, Faction.OfMechanoids);
            GenSpawn.Spawn(mech, new IntVec3(60, 0, 61), map);
            fixtures.Add(mech);
            Check(ability.GetRadiusForPawn() == 5 && !ability.ValidateTarget(mech, false),
                "five area purchases give exactly five tiles and Consume still rejects mechs");
            ability.Cast(new RimWorld.Planet.GlobalTargetInfo(first));
            Check(first.Dead && ally.Dead && !outside.Dead && !mech.Dead && !officiant.Dead,
                "late Consume devours nearby organic allies but spares outside targets and non-organics");
            Check(idol.TotalDevotion > 5000, "the entire Consume bill is paid before victims grant devotion");
            idol.Progress.Import(BudUpgradeKind.ConsumeArea, 10);
            Check(ability.GetRadiusForPawn() == 10, "the maw can deliberately be widened to ten tiles");
        }

        private static void NonOrganic()
        {
            Reset();
            caster.ChangePsylinkLevel(1, false);
            foreach (var kind in DefDatabase<PawnKindDef>.AllDefsListForReading.Where(k =>
                k == PawnKindDefOf.Mech_Scyther || k.race.defName == "VPE_RockConstruct"
                || k.race.defName == "TM_StoneGolem").GroupBy(k => k.race).Select(g => g.First()).ToArray())
            {
                var target = PawnGenerator.GeneratePawn(kind, Faction.OfMechanoids);
                var link = target.AllComps.FirstOrDefault(c => c.GetType().FullName == "VanillaPsycastsExpanded.CompBreakLink");
                if (link != null) AccessTools.Field(link.GetType(), "Pawn").SetValue(link, caster);
                GenSpawn.Spawn(target, new IntVec3(55, 0, 55), map);
                var color = target.AllComps.FirstOrDefault(c => c.GetType().Name == "CompSetStoneColour");
                if (color != null) AccessTools.Method(color.GetType(), "SetStoneColour")
                    .Invoke(color, new object[] { DefDatabase<ThingDef>.GetNamed("ChunkGranite") });
                fixtures.Add(target);
                Check(DeepTargetUtility.IsNonOrganic(target) && !DeepTargetUtility.IsEdible(target)
                    && DeepTargetUtility.IsCombatTarget(target, caster), "physical combat and edible-flesh policies differ for " + kind.defName);
                idol.Progress.Import(BudUpgradeKind.NonOrganicTargets, 1);
                Check(DeepTargetUtility.BudCanAttack(caster, target), "paid brood targeting admits " + kind.defName);
                var grasp = new Ability_Grasp { pawn = caster, holder = caster,
                    def = DefDatabase<VEF.Abilities.AbilityDef>.GetNamed("Deepcaller_Grasp") };
                grasp.Init();
                Check(grasp.ValidateTarget(target, false), "Grasp targeting accepts " + kind.defName);
                grasp.Cast(new RimWorld.Planet.GlobalTargetInfo(target));
                Check(target.health.hediffSet.hediffs.Any(h => h is Hediff_Grasped), "Grasp actually holds " + kind.defName);
                var ink = new Ability_InkVeil { pawn = caster, holder = caster,
                    def = DefDatabase<VEF.Abilities.AbilityDef>.GetNamed("Deepcaller_InkVeil") };
                ink.Init();
                ink.Cast(new RimWorld.Planet.GlobalTargetInfo(target));
                Check(target.health.hediffSet.GetFirstHediffOfDef(Deepcaller_DefOf.Deepcaller_Inked) != null,
                    "Ink actually coats " + kind.defName);
            }
        }

        private static void Compatibility()
        {
            Log.Message("DEEPCALLER_RUNTIME: testing the player's active mod list in an isolated profile");
            NonOrganic();
            Reset();
            var victim = Person(58, 60);
            var comp = victim.AllComps.FirstOrDefault(c => c.GetType().FullName == "IsekaiLeveling.IsekaiComponent");
            Check(comp != null, "installed Isekai component is available for boss-worth integration");
            AccessTools.Field(comp.GetType(), "currentLevel").SetValue(comp, 1600);
            Check(ConsumeWorth.Level(victim) == 1600, "Consume reads the installed Isekai level directly");
            var ability = ConsumeAbility(caster);
            Check(ability.HeatFor(victim, 5000, 5, 5) > 1000,
                "an installed level-1,600 Isekai pawn remains a lethal offering at 5,000 devotion");
            Vehicles();
        }

        private static void Vehicles()
        {
            Reset();
            var vehicleDef = DefDatabase<ThingDef>.GetNamedSilentFail("VVE_Bulldog");
            if (vehicleDef == null) { Log.Message("DEEPCALLER_RUNTIME: vehicle fixture not installed; skipped"); return; }
            var spawner = AccessTools.TypeByName("Vehicles.VehicleSpawner");
            var generate = AccessTools.Method(spawner, "GenerateVehicle", new[] {
                AccessTools.TypeByName("Vehicles.VehicleDef"), typeof(Faction) });
            var vehicle = (Pawn)generate.Invoke(null, new object[] { vehicleDef, Faction.OfPlayer });
            GenSpawn.Spawn(vehicle, new IntVec3(70, 0, 60), map);
            fixtures.Add(vehicle);
            Check(RiptideVehicleBridge.IsVehicle(vehicle) && Thing_RiptideSurge.MassOf(vehicle) >= 450,
                "Vehicle Framework mass is used instead of the ordinary pawn stat");
            Check(RiptideVehicleBridge.ProjectedCells(vehicle, vehicle.Position).Count() == 15,
                "vehicle collisions use its full 3 by 5 footprint");
            idol.Progress.Import(BudUpgradeKind.RiptideDiscernment, 1);
            Check(RiptideImpactUtility.AffectsThing(vehicle, caster), "people favor does not protect a vehicle");
            idol.Progress.Import(BudUpgradeKind.RiptidePossessions, 1);
            Check(!RiptideImpactUtility.AffectsThing(vehicle, caster), "possessions favor keeps an owned vehicle grounded");
            DeepcallerGameComponent.Instance.cultivation = new CultivationProgress();
            var oldPosition = vehicle.Position;
            Advance(Cast(new List<Thing> { vehicle }, new List<Thing> { vehicle }, 0), 30);
            Check(vehicle.Position.x < Center.x && !vehicle.Dead, "vehicle coasts beyond the center through its own teleport integration");
            RiptideVehicleBridge.TurnAround(vehicle);
            Check(RiptideVehicleBridge.ProjectedCells(vehicle, vehicle.Position).Count() == 15,
                "turning preserves vehicle footprint");
            vehicle.Position = oldPosition;
            RiptideVehicleBridge.NotifyMoved(vehicle);
            var wall = ThingMaker.MakeThing(ThingDefOf.Wall, ThingDefOf.WoodLog);
            wall.SetFaction(Faction.OfPlayer);
            GenSpawn.Spawn(wall, new IntVec3(68, 0, 62), map);
            fixtures.Add(wall);
            hitThings.Clear();
            Advance(Cast(new List<Thing> { vehicle }, new List<Thing> { vehicle, wall }, 8000), 3);
            Check(hitThings.Contains(vehicle) && hitThings.Contains(wall),
                "off-center vehicle footprint collision applies damage to both vehicle and wall before armor");
        }
    }
}
