using RimWorld;
using Verse;
using Verse.AI;

namespace Deepcaller
{
    /// The Leviathan's head trails its summoner between fights, dragging the
    /// whole tethered formation along. Not JobGiver_AIFollowPawn: that base
    /// logs an error whenever the followee is null, and ours legitimately is
    /// once the summoner dies (the wander node takes over then).
    public class JobGiver_FollowSummoner : ThinkNode_JobGiver
    {
        public float followRadius = 4.9f;

        protected override Job TryGiveJob(Pawn pawn)
        {
            var summoner = pawn.TryGetComp<CompLeviathanCore>()?.summoner;
            if (summoner == null || summoner.Dead || !summoner.Spawned
                || summoner.Map != pawn.Map
                || !pawn.CanReach(summoner, PathEndMode.OnCell, Danger.Deadly)
                || !JobDriver_FollowClose.FarEnoughAndPossibleToStartJob(pawn, summoner, followRadius))
                return null;

            var job = JobMaker.MakeJob(JobDefOf.FollowClose, summoner);
            job.expiryInterval = 200;
            job.checkOverrideOnExpire = true;
            job.followRadius = followRadius;
            return job;
        }
    }

    /// Arms walk after their head. The tether pull in CompLeviathanPart
    /// drags along a straight sight line and stops dead at the first wall,
    /// so without real pathfinding an arm left behind a corner froze
    /// forever. Inert for unlinked tentacles (core == null).
    public class JobGiver_FollowLeviathanCore : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            var part = pawn.TryGetComp<CompLeviathanPart>();
            var core = part?.core;
            if (core == null || core == pawn || core.Dead || !core.Spawned
                || core.Map != pawn.Map)
                return null;

            var radius = UnityEngine.Mathf.Max(part.Props.tetherRadius - 1f, 1f);
            if (!pawn.CanReach(core, PathEndMode.OnCell, Danger.Deadly)
                || !JobDriver_FollowClose.FarEnoughAndPossibleToStartJob(pawn, core, radius))
                return null;

            var job = JobMaker.MakeJob(JobDefOf.FollowClose, core);
            job.expiryInterval = 140;
            job.checkOverrideOnExpire = true;
            job.followRadius = radius;
            return job;
        }
    }
}
