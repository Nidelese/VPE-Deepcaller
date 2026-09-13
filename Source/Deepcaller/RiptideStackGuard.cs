using System.Linq;
using HarmonyLib;
using Verse;

namespace Deepcaller
{
    // The compressor normally discards IDs for plain chunks/rocks/items.
    // Frozen rosters and in-flight body references must retain those IDs.
    [HarmonyPatch(typeof(CompressibilityDeciderUtility), nameof(CompressibilityDeciderUtility.IsSaveCompressible))]
    public static class RiptideSaveGuard
    {
        public static void Postfix(Thing t, ref bool __result)
        {
            if (!__result || t.Map == null) return;
            foreach (var surge in t.Map.listerThings.ThingsOfDef(DefDatabase<ThingDef>.GetNamed("Deepcaller_RiptideSurge")))
                if (surge is Thing_RiptideSurge current && current.Captured(t)) { __result = false; return; }
        }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.TryAbsorbStack))]
    public static class RiptideStackGuard
    {
        public static bool Prefix(Thing __instance, Thing other, ref bool __result)
        {
            var map = __instance.MapHeld ?? other?.MapHeld;
            if (map == null || other == null) return true;
            // Do not let fresh drops merge into a captured stack and inherit
            // its motion/damage. Normal stacking resumes when the cast ends.
            foreach (var surge in map.listerThings.ThingsOfDef(DefDatabase<ThingDef>.GetNamed("Deepcaller_RiptideSurge"))
                .OfType<Thing_RiptideSurge>())
                if (surge.Captured(__instance) || surge.Captured(other))
                {
                    __result = false;
                    return false;
                }
            return true;
        }
    }
}
