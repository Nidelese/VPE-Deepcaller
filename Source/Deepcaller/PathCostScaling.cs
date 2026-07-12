using HarmonyLib;
using VanillaPsycastsExpanded;
using Verse;

namespace Deepcaller
{
    /// Put on a PsycasterPathDef: psyfocus costs for that path's psycasts
    /// are multiplied by this curve evaluated at the caster's psylink level.
    /// Psyfocus is a fixed 0-100% pool that never grows with progression,
    /// so %-costs mean a level-30 archon has the same endurance as a novice
    /// — hopeless against modded raid bosses with huge HP pools. The curve
    /// makes casts pricey early and cheap for a master.
    public class PathCostScalingExtension : DefModExtension
    {
        public SimpleCurve psyfocusCostFactorByLevel;
    }

    /// GetPsyfocusUsedByPawn is VPE's single choke point for psyfocus costs
    /// (gating, gizmo display, and the deduction all call it), so one
    /// postfix scales everything consistently.
    [HarmonyPatch(typeof(AbilityExtension_Psycast), nameof(AbilityExtension_Psycast.GetPsyfocusUsedByPawn))]
    public static class AbilityExtension_Psycast_GetPsyfocusUsedByPawn_Patch
    {
        public static void Postfix(AbilityExtension_Psycast __instance, Pawn pawn, ref float __result)
        {
            var curve = __instance.path?.GetModExtension<PathCostScalingExtension>()?.psyfocusCostFactorByLevel;
            if (curve == null)
                return;
            var psycasts = pawn.Psycasts();
            if (psycasts == null)
                return;
            __result *= curve.Evaluate(psycasts.level);
        }
    }
}
