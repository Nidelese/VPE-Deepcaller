using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Deepcaller
{
    /// The caller screams into the ground and the whole garden surges:
    /// every friendly tentacle on the map advances one growth stage
    /// immediately (with the usual stage-up regeneration).
    public class Ability_Overgrowth : VEF.Abilities.Ability
    {
        public override bool IsEnabledForPawn(out string reason)
        {
            if (!AnyTentacle())
            {
                reason = "Deepcaller_OvergrowthNoTentacles".Translate();
                return false;
            }
            return base.IsEnabledForPawn(out reason);
        }

        public override void Cast(params GlobalTargetInfo[] targets)
        {
            base.Cast(targets);
            var map = pawn.Map;
            foreach (var other in map.mapPawns.SpawnedPawnsInFaction(pawn.Faction))
            {
                var growth = other.TryGetComp<CompTentacleGrowth>();
                if (growth == null || growth.FullyGrown)
                    continue;
                growth.GrantStage(growth.Stage + 1);
                FleckMaker.ThrowDustPuffThick(other.DrawPos, map, 2f, DeepPullUtility.DeepColor);
            }
        }

        private bool AnyTentacle()
        {
            var map = pawn.MapHeld;
            if (map == null)
                return false;
            foreach (var other in map.mapPawns.SpawnedPawnsInFaction(pawn.Faction))
                if (other.TryGetComp<CompTentacleGrowth>() != null)
                    return true;
            return false;
        }
    }
}
