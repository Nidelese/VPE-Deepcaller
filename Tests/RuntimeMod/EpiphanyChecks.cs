using System;
using System.Collections.Generic;
using System.Linq;
using Deepcaller;
using HarmonyLib;
using RimWorld;
using VanillaPsycastsExpanded;
using Verse;
using Verse.AI;

namespace DeepcallerRuntimeChecks
{
    public static class EpiphanyChecks
    {
        private static readonly List<(Pawn pawn, float chance, string reason)> rolls = new List<(Pawn, float, string)>();
        private static bool suppress;
        private static Pawn forceSuccess, observed;
        private static int focusQueries;
        private static int graftId, acceptedId, refusedId, pendingId, sourceId, callerId;
        private const int SavedCycle = 864000;
        private static EpiphanyGameComponent Component => EpiphanyGameComponent.Instance;
        private static MeditationFocusDef Focus => DefDatabase<MeditationFocusDef>.GetNamed("Deepcaller_Deep");

        public static bool ObserveRoll(Pawn pawn, ref float chance, string reason, ref bool __result)
        {
            rolls.Add((pawn, chance, reason));
            if (suppress) { __result = false; return false; }
            if (pawn == forceSuccess) chance = 1;
            return true;
        }
        public static void ObserveFocus(Pawn p) { if (p == observed) focusQueries++; }
        private static void Probe(Action action)
        {
            rolls.Clear();
            focusQueries = 0;
            suppress = true;
            try { action(); }
            finally { suppress = false; }
        }
        private static Pawn Person(Map map, int x = 80, int z = 80)
        {
            var pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,
                Faction.OfPlayer, forceGenerateNewPawn: true, canGeneratePawnRelations: false, forceNoIdeo: true));
            GenSpawn.Spawn(pawn, new IntVec3(x, 0, z), map);
            return pawn;
        }
        private static Pawn Tentacle(Map map, Pawn caller, int x = 90, int z = 90)
        {
            var pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                DefDatabase<PawnKindDef>.GetNamed("Deepcaller_Tentacle"), Faction.OfPlayer,
                forceGenerateNewPawn: true, canGeneratePawnRelations: false, fixedBiologicalAge: 0));
            GenSpawn.Spawn(pawn, new IntVec3(x, 0, z), map);
            Component.BindSummon(pawn, caller);
            return pawn;
        }
        private static void SetBelief(Pawn pawn, params string[] memes)
        {
            var ideo = IdeoGenerator.GenerateIdeo(new IdeoGenerationParms(Faction.OfPlayer.def));
            ideo.name = "Epiphany fixture";
            ideo.memes.Clear();
            foreach (string name in memes) ideo.memes.Add(DefDatabase<MemeDef>.GetNamed(name));
            Find.IdeoManager.Add(ideo);
            pawn.ideo.SetIdeo(ideo);
        }
        private static ChoiceLetter_AbyssalEpiphany Invitation(Pawn pawn) =>
            Find.LetterStack.LettersListForReading.OfType<ChoiceLetter_AbyssalEpiphany>().Single(l => l.pawn == pawn);
        private static void ForceOffer(Pawn pawn)
        {
            forceSuccess = pawn;
            try { Component.TryRoll(pawn, 0.01f, "Deepcaller_EpiphanyOffering"); }
            finally { forceSuccess = null; }
        }

        public static void Run(Map map, Action<bool, string> check)
        {
            var harmony = new Harmony("Deepcaller.RuntimeChecks.Epiphanies");
            harmony.Patch(AccessTools.Method(typeof(EpiphanyGameComponent), nameof(EpiphanyGameComponent.TryRoll)),
                prefix: new HarmonyMethod(typeof(EpiphanyChecks), nameof(ObserveRoll)));
            harmony.Patch(AccessTools.Method(typeof(MeditationFocusTypeAvailabilityCache), nameof(MeditationFocusTypeAvailabilityCache.PawnCanUse)),
                prefix: new HarmonyMethod(typeof(EpiphanyChecks), nameof(ObserveFocus)));
            for (int x = 70; x <= 110; x++)
            for (int z = 70; z <= 110; z++)
            {
                var cell = new IntVec3(x, 0, z);
                foreach (var thing in cell.GetThingList(map).ToArray()) if (thing.def.destroyable) thing.Destroy();
                map.roofGrid.SetRoof(cell, null);
                map.terrainGrid.SetTerrain(cell, TerrainDefOf.Soil);
            }
            var grafted = Person(map);
            graftId = grafted.thingIDNumber;
            check(Component.IsCandidate(grafted) && !Focus.CanPawnUse(grafted), "new locked colonists enroll for epiphanies");
            var caller = Person(map, 71, 71);
            callerId = caller.thingIDNumber;
            var source = Tentacle(map, caller);
            sourceId = source.thingIDNumber;
            Probe(() => Component.NotifySleep(grafted, SavedCycle));
            check(rolls.Count == 0, "nearby summoned tentacles do not grant graft sleep rolls");
            var symbiote = (Hediff_Symbiote)grafted.health.AddHediff(Deepcaller_DefOf.Deepcaller_Symbiote);
            symbiote.AddGraft(25, 0);
            symbiote.AddGraft(25, 0);
            Probe(() => Component.NotifySleep(grafted, SavedCycle));
            check(rolls.Count == 1 && Math.Abs(rolls[0].chance - 0.0975f) < 1e-6,
                "two attached grafts give 9.75 percent per sleep cycle");
            Probe(() => Component.NotifySleep(grafted, SavedCycle));
            check(rolls.Count == 0, "repeated sleep checks cannot reroll the same night");

            var faithful = Person(map, 75, 75);
            SetBelief(faithful, "VME_Structure_ChthonianCult", "VME_Structure_Esotericism");
            Probe(() => Component.NotifySleep(faithful, SavedCycle));
            check(rolls.Count == 1 && rolls[0].chance == 0.005f, "installed cult and esoteric memes give one 0.5 percent sleep roll");
            var driver = new JobDriver_Meditate { pawn = faithful, job = JobMaker.MakeJob(JobDefOf.Meditate) };
            Probe(() => driver.Cleanup(JobCondition.Succeeded));
            check(rolls.Count == 1 && rolls[0].chance == 0.005f, "completed meditation uses the live job cleanup hook");
            Probe(() => driver.Cleanup(JobCondition.InterruptForced));
            check(rolls.Count == 0, "interrupted reflection grants no roll");
            SetBelief(faithful, "PainIsVirtue");
            Probe(() => Component.NotifyReflection(faithful));
            check(rolls.Count == 0, "Pain is virtue alone is not an affinity");

            Probe(() =>
            {
                Component.NotifyCombat(source, true, 0);
                Component.NotifyCombat(source, true, 100);
                Component.NotifyCombat(source, false, 2000);
                Component.NotifyCombat(source, true, 3000);
            });
            check(rolls.Count == 1 && rolls[0].pawn == caller && rolls[0].chance == 0.01f,
                "a tentacle rolls once per fight and incoming damage keeps the fight active");
            Probe(() => Component.NotifyCombat(source, true, 5500));
            check(rolls.Count == 1, "a quiet game hour permits a new fight roll");
            var enemy = PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Mech_Scyther,
                Faction.OfMechanoids, forceGenerateNewPawn: true));
            GenSpawn.Spawn(enemy, new IntVec3(90, 0, 91), map);
            Probe(() => enemy.TakeDamage(new DamageInfo(DamageDefOf.Blunt, 1, 0, -1, source)));
            // Above synthetic times are ahead of the real clock; use a fresh tentacle for the live hook.
            var liveSource = Tentacle(map, caller, 89, 90);
            Probe(() => enemy.TakeDamage(new DamageInfo(DamageDefOf.Blunt, 1, 100, -1, liveSource)));
            check(rolls.Count == 1 && rolls[0].pawn == caller, "real tentacle damage against a mech triggers its caller's combat roll");

            var meatPawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,
                Faction.OfPlayer, forceGenerateNewPawn: true, canGeneratePawnRelations: false));
            GenSpawn.Spawn(meatPawn, new IntVec3(91, 0, 90), map);
            meatPawn.Kill(null);
            var corpse = meatPawn.Corpse;
            var near = Person(map, 100, 90);
            var far = Person(map, 101, 90);
            Probe(() => Component.NotifyCorpseEaten(source, corpse));
            check(rolls.Any(r => r.pawn == near && r.chance == 0.01f) && rolls.All(r => r.pawn != far),
                "corpse epiphanies include exactly ten tiles and exclude eleven");
            SetBelief(near);
            near.Ideo.AddPrecept(PreceptMaker.MakePrecept(DefDatabase<PreceptDef>.GetNamed("Cannibalism_Abhorrent")));
            check(EpiphanyGameComponent.OfferingDispleases(near, corpse), "actual anti-cannibal precept blocks a human offering");
            forceSuccess = near;
            try { Component.NotifyCorpseEaten(source, corpse); }
            finally { forceSuccess = null; }
            check(Component.IsCandidate(near), "a successful chance roll still respects ideological disgust");

            var accepted = Person(map, 73, 73);
            acceptedId = accepted.thingIDNumber;
            ForceOffer(accepted);
            check(!Focus.CanPawnUse(accepted) && !Component.IsCandidate(accepted), "pending invitation gives no power and suspends further processing");
            var letter = Invitation(accepted);
            check(!letter.Text.ToString().Contains("{REASON}") && letter.Choices.Count() == 3,
                "invitation explains its cause and offers accept, refusal and postponement");
            letter.Choices.First().action();
            check(Focus.CanPawnUse(accepted) && accepted.Psycasts() == null, "acceptance grants only the focus, without a psylink");
            observed = accepted;
            Probe(() =>
            {
                Component.NotifySleep(accepted, SavedCycle);
                Component.NotifyReflection(accepted);
                Component.BindSummon(source, accepted);
                Component.NotifyCombat(source, true, 5600);
                Component.NotifyCorpseEaten(source, corpse);
            });
            check(rolls.All(r => r.pawn != accepted) && focusQueries == 0,
                "unlocked pawns get neither event rolls nor repeated focus eligibility queries");
            observed = null;
            accepted.ChangePsylinkLevel(1, false);
            int points = accepted.Psycasts().points;
            check(Focus.CanPawnUse(accepted) && points > 0, "later psylink creation retains the free revelation without spending points");

            var refused = Person(map, 74, 74);
            refusedId = refused.thingIDNumber;
            ForceOffer(refused);
            Invitation(refused).Choices.ElementAt(1).action();
            check(!Focus.CanPawnUse(refused) && !Component.IsCandidate(refused), "refusal prevents further offers without granting access");
            check(Focus.CanUnlock(refused, out _), "refusal does not block the ordinary point purchase");
            var pending = Person(map, 76, 76);
            pendingId = pending.thingIDNumber;
            ForceOffer(pending);
            var paid = Person(map, 77, 77);
            paid.ChangePsylinkLevel(1, false);
            paid.Psycasts().UnlockMeditationFocus(Focus);
            check(!Component.IsCandidate(paid), "VPE point unlock immediately removes a pawn from epiphany processing");
            Component.RebuildCandidates();
            check(!Component.IsCandidate(accepted) && !Component.IsCandidate(refused) && !Component.IsCandidate(pending)
                && !Component.IsCandidate(paid) && Component.IsCandidate(grafted), "candidate rebuild excludes learned, refused and pending pawns");
        }

        public static void VerifyReload(Map map, Action<bool, string> check)
        {
            Pawn Get(int id) => map.mapPawns.AllPawns.Single(p => p.thingIDNumber == id);
            var accepted = Get(acceptedId);
            check(Focus.CanPawnUse(accepted) && Component.HasRevelation(accepted) && !Component.IsCandidate(accepted),
                "revelation and processing exclusion survive real game reload");
            check(!Focus.CanPawnUse(Get(refusedId)) && !Component.IsCandidate(Get(refusedId)), "refusal survives game reload");
            check(!Component.IsCandidate(Get(pendingId)) && Invitation(Get(pendingId)) != null, "pending choice letter survives reload without duplicate enrollment");
            Probe(() => Component.NotifySleep(Get(graftId), SavedCycle));
            check(rolls.Count == 0, "loading during a sleep cycle cannot reroll its graft chance");
            Probe(() => Component.NotifySleep(Get(graftId), SavedCycle + GenDate.TicksPerDay));
            check(rolls.Count == 1, "the following sleep cycle still gets its chance after reload");
            Probe(() => Component.NotifyCombat(Get(sourceId), true, 5700));
            check(rolls.Count == 0, "loading during combat cannot repeat a tentacle's fight roll");
            Probe(() => Component.NotifyCombat(Get(sourceId), true, 9000));
            check(rolls.Count == 1 && rolls[0].pawn == Get(callerId), "summoner binding and the next fight survive reload");
        }
    }
}
