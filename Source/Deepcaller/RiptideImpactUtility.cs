using RimWorld;
using System.Linq;
using UnityEngine;
using Verse;

namespace Deepcaller
{
    public static class RiptideImpactUtility
    {
        public static bool IsNaturalRock(Thing thing) => thing is Mineable && thing.def.building != null
            && (thing.def.building.isNaturalRock || thing.def.building.mineableThing != null
                || thing.def.defName == "CollapsedRocks" || thing.def.defName == "RaisedRocks");

        public static bool LocalIdolSated(Map map, Faction faction) =>
            CompIdolDevotion.BestIdol(map, faction) is { Hungry: false };

        public static bool ProtectsStructures(Pawn caster) => caster != null &&
            RiptideImpactRules.FavorActive(Cultivation.Rank(caster, BudUpgradeKind.RiptideRestraint),
                LocalIdolSated(caster.MapHeld, caster.Faction));

        public static bool TargetsHostilesOnly(Pawn caster) => caster != null &&
            RiptideImpactRules.FavorActive(Cultivation.Rank(caster, BudUpgradeKind.RiptideDiscernment),
                LocalIdolSated(caster.MapHeld, caster.Faction));

        public static bool ProtectsPossessions(Pawn caster) => caster != null &&
            RiptideImpactRules.FavorActive(Cultivation.Rank(caster, BudUpgradeKind.RiptidePossessions),
                LocalIdolSated(caster.MapHeld, caster.Faction));

        public static bool IsPlayerBarrier(Thing target) => target is Building && target.Faction == Faction.OfPlayer
            && (target is Building_Door || target.def.Fillage == FillCategory.Full);

        public static bool IsPlayerPossession(Thing target) => target.Faction == Faction.OfPlayer
            || target is Corpse corpse && corpse.InnerPawn?.Faction == Faction.OfPlayer
            // A home-area mark or a haul order does not claim scattered chunks.
            // Deliberately stored, accepted items count as managed possessions.
            || target.def.category == ThingCategory.Item && InPlayerStorage(target);

        private static bool InPlayerStorage(Thing target)
        {
            var storage = StoreUtility.CurrentHaulDestinationOf(target);
            return storage != null && storage.Accepts(target)
                && (storage is Zone_Stockpile || storage is Building building && building.Faction == Faction.OfPlayer);
        }

        public static bool IsMovable(Thing target) => target != null && target.Spawned && !target.Destroyed
            && (target is Pawn pawn ? !pawn.Dead : target.def.category == ThingCategory.Item);

        public static bool AffectsPawn(Pawn target, Pawn caster) => AffectsThing(target, caster);

        public static bool AffectsThing(Thing target, Pawn caster)
        {
            if (!IsMovable(target)) return false;
            // Vehicles are possessions, even though the framework represents
            // them as pawns. Non-hostile creatures use the people favor.
            bool creature = target is Pawn && !RiptideVehicleBridge.IsVehicle(target);
            int rank = Cultivation.Rank(caster, creature ? BudUpgradeKind.RiptideDiscernment : BudUpgradeKind.RiptidePossessions);
            if (rank == 0 || caster == null || !LocalIdolSated(target.Map, caster.Faction)) return true;
            return creature ? target.HostileTo(caster) : !IsPlayerPossession(target);
        }

        public static RiptideImpact Resolve(Thing body, Thing obstacle, Pawn caster)
        {
            if (obstacle is not Building || !obstacle.Spawned)
                return RiptideImpact.None;
            bool bodyAtObstacle = IsMovable(body) && body.Map == obstacle.Map
                && RiptideVehicleBridge.ProjectedCells(body, body.Position)
                    .Any(cell => cell.AdjacentTo8WayOrInside(obstacle));
            return RiptideImpactRules.Resolve(bodyAtObstacle, IsNaturalRock(obstacle),
                IsPlayerBarrier(obstacle), Cultivation.Rank(caster, BudUpgradeKind.RiptideRestraint),
                caster != null && LocalIdolSated(obstacle.Map, caster.Faction));
        }

        public static bool CanDamage(RiptideImpact impact) => impact == RiptideImpact.Mine || impact == RiptideImpact.Smash;

        public static void Apply(Thing body, Thing obstacle, float pressure, Pawn caster)
        {
            // Recheck the live body, ownership and hunger at impact time.
            var impact = Resolve(body, obstacle, caster);
            if (!CanDamage(impact) || pressure <= 0 || float.IsNaN(pressure)) return;
            // Mining damage uses normal ore-yield, fog and roof handling.
            obstacle.TakeDamage(new DamageInfo(impact == RiptideImpact.Mine ? DamageDefOf.Mining : DamageDefOf.Blunt,
                Mathf.Min(RiptideMath.MaximumDamage, pressure), 0, -1, caster));
        }
    }
}
