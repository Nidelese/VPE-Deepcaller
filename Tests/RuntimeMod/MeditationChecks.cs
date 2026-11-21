using System;
using System.Linq;
using Deepcaller;
using RimWorld;
using VanillaPsycastsExpanded;
using Verse;

namespace DeepcallerRuntimeChecks
{
    public static class MeditationChecks
    {
        private static int learnedId, unlearnedId;

        public static void Run(Map map, Action<bool, string> check)
        {
            var settings = DeepcallerMod.Settings;
            check(!settings.automaticAbyssalMeditation, "fresh settings default automatic abyssal meditation off");
            var focus = DefDatabase<MeditationFocusDef>.GetNamed("Deepcaller_Deep");
            var pawn = GeneratePawn();
            GenSpawn.Spawn(pawn, CellFinder.RandomClosewalkCellNear(map.Center, map, 10), map);
            learnedId = pawn.thingIDNumber;
            pawn.story.Childhood = DefDatabase<BackstoryDef>.AllDefsListForReading.First(b =>
                b.slot == BackstorySlot.Childhood && b.spawnCategories.Contains("Tribal"));
            MeditationFocusTypeAvailabilityCache.ClearFor(pawn);
            check(!focus.CanPawnUse(pawn), "ordinary tribal cannot use abyssal meditation for free");
            var idol = ThingMaker.MakeThing(Deepcaller_DefOf.Deepcaller_Idol).TryGetComp<CompMeditationFocus>();
            check(!idol.CanPawnUse(pawn), "idol meditation eligibility rejects an uninitiated pawn");
            var others = DefDatabase<MeditationFocusDef>.AllDefsListForReading
                .Where(f => f != focus).ToDictionary(f => f, f => f.CanPawnUse(pawn));

            settings.automaticAbyssalMeditation = true;
            check(focus.CanPawnUse(pawn) && idol.CanPawnUse(pawn), "opting in grants existing pawns access immediately");
            check(others.All(pair => pair.Key.CanPawnUse(pawn) == pair.Value), "automatic access does not change other meditation types");
            check(focus.EnablingThingsExplanation(pawn).Contains("Deepcaller_MeditationEnabledBySetting".Translate()),
                "free-access explanation names the enabled setting");
            settings.automaticAbyssalMeditation = false;
            check(!focus.CanPawnUse(pawn) && !idol.CanPawnUse(pawn), "opting out immediately removes unearned access without a cache reset");

            pawn.ChangePsylinkLevel(1, false);
            var psycasts = pawn.Psycasts();
            psycasts.UnlockPath(DefDatabase<PsycasterPathDef>.GetNamed("Deepcaller_Path"));
            check(!focus.CanPawnUse(pawn), "a psylink and the Deepcaller path do not grant free abyssal meditation");
            check(focus.CanUnlock(pawn, out _), "abyssal meditation remains available to learn through VPE");
            psycasts.points = 1;
            // The same two operations used by VPE's focus unlock button.
            psycasts.SpentPoints();
            psycasts.UnlockMeditationFocus(focus);
            check(psycasts.points == 0 && focus.CanPawnUse(pawn) && idol.CanPawnUse(pawn),
                "spending one psycast point unlocks actual idol meditation");
            settings.automaticAbyssalMeditation = true;
            focus.CanPawnUse(pawn);
            settings.automaticAbyssalMeditation = false;
            check(focus.CanPawnUse(pawn), "paid meditation survives turning automatic access off");

            var other = GeneratePawn();
            GenSpawn.Spawn(other, CellFinder.RandomClosewalkCellNear(map.Center, map, 10), map);
            unlearnedId = other.thingIDNumber;
            check(!focus.CanPawnUse(other), "one pawn's purchase does not unlock another pawn");
            foreach (bool enabled in new[] { false, true })
            {
                var original = new DeepcallerSettings { automaticAbyssalMeditation = enabled };
                string path = System.IO.Path.Combine(GenFilePaths.SaveDataFolderPath, "MeditationSettingsTest.xml");
                Scribe.saver.InitSaving(path, "MeditationSettingsTest");
                Scribe_Deep.Look(ref original, "settings");
                Scribe.saver.FinalizeSaving();
                DeepcallerSettings restored = null;
                Scribe.loader.InitLoading(path);
                Scribe_Deep.Look(ref restored, "settings");
                Scribe.loader.FinalizeLoading();
                check(restored != null && restored.automaticAbyssalMeditation == enabled,
                    "mod settings round-trip with automatic access " + enabled);
            }
        }

        // These fixtures have no family role. Random relation generation can
        // link quicktest world pawns to a discarded generation attempt.
        private static Pawn GeneratePawn() => PawnGenerator.GeneratePawn(new PawnGenerationRequest(
            PawnKindDefOf.Colonist, Faction.OfPlayer, forceGenerateNewPawn: true, canGeneratePawnRelations: false));

        public static void VerifyReload(Map map, Action<bool, string> check)
        {
            var focus = DefDatabase<MeditationFocusDef>.GetNamed("Deepcaller_Deep");
            var learned = map.mapPawns.AllPawns.Single(p => p.thingIDNumber == learnedId);
            var unlearned = map.mapPawns.AllPawns.Single(p => p.thingIDNumber == unlearnedId);
            check(focus.CanPawnUse(learned) && learned.Psycasts().points == 0,
                "game reload preserves paid abyssal meditation and the spent point");
            check(!focus.CanPawnUse(unlearned), "game reload does not grant unearned abyssal meditation");
        }
    }
}
