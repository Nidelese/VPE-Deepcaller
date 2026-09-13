using System;
using System.Linq;
using RimWorld;
using Verse;

namespace Deepcaller
{
    public partial class CompIdolDevotion
    {
        // Engine limits must never turn into a paid rank with no possible
        // improvement. Re-evaluate map-dependent limits when the world changes.
        public bool UpgradeSaturated(BudUpgradeKind kind)
        {
            int rank = Progress?.Rank(kind) ?? 0;
            if (rank == int.MaxValue) return true;
            if (kind == BudUpgradeKind.RiptideArea || kind == BudUpgradeKind.RiptideReach || kind == BudUpgradeKind.ConsumeArea) return RangeCoversMap(kind);
            if (kind == BudUpgradeKind.ConsumeBurn) return ConsumeMath.Heat(ConsumeMath.RawHeat(
                float.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue, int.MaxValue), TotalDevotion, rank,
                parent.MapHeld?.Size.LengthHorizontal ?? 350) <= 0.0001;
            if (kind == BudUpgradeKind.RiptideAcceleration)
            {
                var ability = DefDatabase<VEF.Abilities.AbilityDef>.GetNamed("Deepcaller_Riptide");
                var ext = ability.GetModExtension<AbilityExtension_Riptide>();
                var last = ext.radiusByDevotion.Points.Last();
                double radius = RiptideMath.MapBoundedReach(RiptideMath.ExtendedRadius(TotalDevotion,
                        ext.radiusByDevotion.Evaluate(Devotion), last.x, last.y),
                    RiptideMath.ReachExtension(TotalDevotion, Progress?.Rank(BudUpgradeKind.RiptideArea) ?? 0, true),
                    parent.MapHeld?.Size.LengthHorizontal ?? 350);
                if (RiptideKinetics.Acceleration(TotalDevotion, 70,
                    CultivationMath.Factor(rank, 0.1f) / radius, ext.ticksPerCell) < RiptideKinetics.MaximumSpeed) return false;
                double mass = parent.MapHeld?.listerThings.AllThings.Where(RiptideImpactUtility.IsMovable)
                    .Select(Thing_RiptideSurge.MassOf).DefaultIfEmpty(70).Max() ?? 70;
                // If even the heaviest body one cell from center reaches the
                // integration ceiling, no current placement benefits further.
                return RiptideKinetics.Acceleration(TotalDevotion, Math.Max(70, mass),
                    CultivationMath.Factor(rank, 0.1f) / radius, ext.ticksPerCell) >= RiptideKinetics.MaximumSpeed;
            }
            if (kind == BudUpgradeKind.MoveSpeed) return CultivationMath.Factor(rank, 0.1f) * 1.2 >= 60;
            if (kind == BudUpgradeKind.HoardRestoration) return HoardMath.Restoration(rank) == HoardMath.Restoration(rank + 1);
            if (kind == BudUpgradeKind.HoardBargaining) return PriceFactor * (double)float.MaxValue <= 1;
            if (Cultivation.RankLimit(kind) == 1) return rank >= 1;
            float step = kind == BudUpgradeKind.Feeding || kind == BudUpgradeKind.Regeneration ? 0.15f : 0.12f;
            return CultivationMath.Factor(rank, step) == CultivationMath.Factor(rank + 1, step);
        }
    }
}
