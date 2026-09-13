using RimWorld;
using Verse;

namespace Deepcaller
{
    public static class DeepTargetUtility
    {
        public static bool IsNonOrganic(Pawn pawn) => pawn != null && PhysicalTargetRules.NonOrganic(
            pawn.RaceProps.IsFlesh, pawn.RaceProps.FleshType.corpseCategory == ThingCategoryDefOf.CorpsesMechanoid,
            pawn.RaceProps.FleshType.defName, pawn.def.defName);

        public static bool IsEdible(Pawn pawn) => pawn != null && !IsNonOrganic(pawn)
            && !pawn.def.HasModExtension<TentacleRaceExtension>();

        // Physical control never asks whether a pawn has flesh, a mind, or
        // psychic sensitivity. Feeding is a separate decision.
        public static bool IsCombatTarget(Pawn target, Thing attacker) => target != null && attacker != null
            && target.Spawned && !target.Dead && attacker.MapHeld == target.Map
            && target != attacker && target.HostileTo(attacker);

        public static bool BudCanAttack(Thing bud, Thing target) => target != null && bud != null
            && target.Spawned && !target.Destroyed && target.Map == bud.MapHeld && target.HostileTo(bud)
            && (target is Pawn victim ? !victim.Dead && !victim.Downed
                && PhysicalTargetRules.BudCanAttack(IsNonOrganic(victim), Cultivation.Rank(bud, BudUpgradeKind.NonOrganicTargets))
                : Cultivation.Rank(bud, BudUpgradeKind.NonOrganicTargets) > 0);
    }
}
