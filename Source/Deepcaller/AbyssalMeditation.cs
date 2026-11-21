using HarmonyLib;
using RimWorld;
using Verse;

namespace Deepcaller
{
    /// <summary>
    /// VPE's pointsOnly focus handles the earned route and its saved unlocks.
    /// Apply the optional free-access setting outside vanilla's availability
    /// cache so toggling it takes effect immediately for every pawn, including
    /// world pawns, without clearing other mods' meditation state.
    /// </summary>
    [HarmonyPatch(typeof(MeditationFocusTypeAvailabilityCache), nameof(MeditationFocusTypeAvailabilityCache.PawnCanUse))]
    public static class AbyssalMeditation
    {
        public static bool IsAbyssal(MeditationFocusDef focus) => focus?.defName == "Deepcaller_Deep";

        public static void Postfix(Pawn p, MeditationFocusDef type, ref bool __result)
        {
            if (IsAbyssal(type) && (DeepcallerMod.Settings?.automaticAbyssalMeditation == true
                || EpiphanyGameComponent.Instance?.HasRevelation(p) == true))
                __result = true;
        }
    }

    [HarmonyPatch(typeof(MeditationFocusDef), nameof(MeditationFocusDef.EnablingThingsExplanation))]
    public static class AbyssalMeditationExplanation
    {
        public static void Postfix(MeditationFocusDef __instance, Pawn pawn, ref string __result)
        {
            if (!AbyssalMeditation.IsAbyssal(__instance)) return;
            string key = EpiphanyGameComponent.Instance?.HasRevelation(pawn) == true
                ? "Deepcaller_MeditationEnabledByEpiphany"
                : DeepcallerMod.Settings?.automaticAbyssalMeditation == true
                    ? "Deepcaller_MeditationEnabledBySetting" : null;
            if (key == null) return;
            if (!__result.NullOrEmpty()) __result += "\n";
            __result += "  - " + key.Translate();
        }
    }
}
