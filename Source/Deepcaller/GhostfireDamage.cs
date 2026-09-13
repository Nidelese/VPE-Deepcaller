using HarmonyLib;
using RimWorld;
using Verse;

namespace Deepcaller
{
    // Route the marker damage before the normal shield/armor/damage pipeline.
    // Flesh keeps the existing Burn behavior; the purchased physical payload
    // hits constructs and machines with blunt force instead of relying on fire.
    [HarmonyPatch(typeof(Thing), nameof(Thing.TakeDamage))]
    public static class GhostfireDamage
    {
        [HarmonyPriority(Priority.First)]
        public static void Prefix(Thing __instance, ref DamageInfo dinfo)
        {
            if (dinfo.Def?.defName != "Deepcaller_Ghostfire") return;
            bool nonOrganic = __instance is not Pawn victim || DeepTargetUtility.IsNonOrganic(victim);
            dinfo.Def = nonOrganic && Cultivation.Rank(dinfo.Instigator, BudUpgradeKind.NonOrganicTargets) > 0
                ? DamageDefOf.Blunt : DamageDefOf.Burn;
        }
    }
}
