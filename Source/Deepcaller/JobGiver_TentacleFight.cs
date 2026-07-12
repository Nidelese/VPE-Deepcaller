using RimWorld;
using Verse;
using Verse.AI;

namespace Deepcaller
{
    /// Mech combat brain with growth-scaled target acquisition: the deep sees
    /// farther as it feeds. When no standing enemies remain, finishes off
    /// downed hostiles instead of letting them crawl away.
    public class JobGiver_TentacleFight : JobGiver_AIFightEnemies
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            // Think nodes are shared between pawns; setting the radii on every
            // invocation (rather than caching) keeps them per-pawn correct.
            var growth = pawn.TryGetComp<CompTentacleGrowth>();
            if (growth != null)
            {
                targetAcquireRadius = growth.AcquireRadius;
                targetKeepRadius = growth.AcquireRadius + 8f;
            }

            // Leviathan arms are leashed to the head: chasing targets beyond
            // the tether just gets them yanked back mid-charge. Only bite what
            // the leash can reach; the head walks them into range.
            var part = pawn.TryGetComp<CompLeviathanPart>();
            if (part?.core != null && part.core != pawn)
            {
                targetAcquireRadius = part.Props.tetherRadius + 3f;
                targetKeepRadius = targetAcquireRadius + 4f;
            }

            var job = base.TryGiveJob(pawn);
            if (job != null)
                return job;

            var downed = FindDownedHostile(pawn, targetAcquireRadius);
            if (downed == null)
                return null;

            var execute = JobMaker.MakeJob(JobDefOf.AttackMelee, downed);
            execute.killIncappedTarget = true;
            execute.expiryInterval = 600;
            return execute;
        }

        private static Pawn FindDownedHostile(Pawn pawn, float radius)
        {
            Pawn best = null;
            var bestDistSq = float.MaxValue;
            foreach (var target in pawn.Map.attackTargetsCache.GetPotentialTargetsFor(pawn))
            {
                if (target.Thing is not Pawn victim || !victim.Downed || victim.Dead)
                    continue;
                var distSq = victim.Position.DistanceToSquared(pawn.Position);
                if (distSq < bestDistSq
                    && victim.Position.InHorDistOf(pawn.Position, radius)
                    && pawn.CanReach(victim, PathEndMode.Touch, Danger.Deadly))
                {
                    best = victim;
                    bestDistSq = distSq;
                }
            }

            return best;
        }
    }
}
