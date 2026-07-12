using HarmonyLib;
using Verse;

namespace Deepcaller
{
    /// The ink veil belongs to the deep: BlindSmoke's flat x0.7 accuracy
    /// factor is hardcoded per-shot in ShotReport, so the deep's own ranged
    /// summons (the Leviathan's sludge spitter) are exempted here. Both
    /// summon races carry TentacleRaceExtension.
    [HarmonyPatch(typeof(ShotReport), nameof(ShotReport.HitReportFor))]
    public static class ShotReport_HitReportFor_DeepSeesThroughInk
    {
        private static readonly AccessTools.StructFieldRef<ShotReport, float> GasFactor =
            AccessTools.StructFieldRefAccess<ShotReport, float>("factorFromCoveringGas");

        public static void Postfix(Thing caster, ref ShotReport __result)
        {
            if (caster is Pawn shooter
                && shooter.def.HasModExtension<TentacleRaceExtension>())
                GasFactor(ref __result) = 1f;
        }
    }

    /// Kills feed the killer's growth comp, scaled by victim body size.
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    public static class Pawn_Kill_FeedTentacle
    {
        public static void Postfix(Pawn __instance, DamageInfo? dinfo)
        {
            if (dinfo?.Instigator is Pawn killer && killer != __instance)
            {
                var growth = killer.TryGetComp<CompTentacleGrowth>();
                growth?.Feed(__instance.BodySize * growth.Props.killFeedFactor);
            }
        }
    }

    /// Taunt: AttackTargetFinder multiplies target scores by this factor, so
    /// boosting it makes enemies engage tentacles instead of ignoring them.
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.TargetPriorityFactor), MethodType.Getter)]
    public static class Pawn_TargetPriorityFactor_Taunt
    {
        public static void Postfix(Pawn __instance, ref float __result)
        {
            var ext = __instance.def.GetModExtension<TentacleRaceExtension>();
            if (ext != null)
                __result *= ext.targetPriorityFactor;
        }
    }
}
