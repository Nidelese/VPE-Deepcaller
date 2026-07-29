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
            var core = pawn.TryGetComp<CompLeviathanCore>();
            var summoner = core?.summoner;
            if (summoner == null || summoner.Dead || !summoner.Spawned
                || summoner.Map != pawn.Map
                || !pawn.CanReach(summoner, PathEndMode.OnCell, Danger.Deadly)
                || !JobDriver_FollowClose.FarEnoughAndPossibleToStartJob(
                    pawn, summoner, followRadius))
                return null;

            if (!core.FormationReadyToAdvance())
                return BriefWait(30);

            var job = JobMaker.MakeJob(JobDefOf.FollowClose, summoner);
            job.expiryInterval = 60;
            job.checkOverrideOnExpire = true;
            job.followRadius = followRadius;
            job.locomotionUrgency = LocomotionUrgency.Walk;
            return job;
        }

        private static Job BriefWait(int ticks)
        {
            var wait = JobMaker.MakeJob(JobDefOf.Wait_Combat);
            wait.expiryInterval = ticks;
            wait.checkOverrideOnExpire = true;
            return wait;
        }
    }

    /// Linked limbs return to their assigned hardpoint after their own combat
    /// brain is done. A real Goto job preserves pathfinding around walls;
    /// CompLeviathanPart handles only emergency pull/resurface behavior.
    public class JobGiver_FollowLeviathanCore : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            var part = pawn.TryGetComp<CompLeviathanPart>();
            var core = part?.core;
            if (core == null || core == pawn || core.Dead || !core.Spawned
                || core.Map != pawn.Map)
                return null;

            var anchor = part.AnchorCell;
            if (!anchor.IsValid)
                return BriefWait(45);

            var tolerance = part.IsFlower ? 0.4f : 0.8f;
            if (pawn.Position.DistanceTo(anchor) <= tolerance)
                return BriefWait(60);

            var resurfaceDistance = part.IsFlower ? 2.4f : 3.8f;
            if (pawn.Position.DistanceTo(anchor) > resurfaceDistance
                || !pawn.CanReach(
                    anchor, PathEndMode.OnCell, Danger.Deadly))
            {
                if (part.TryResurfaceAtAnchor())
                    return BriefWait(20);
                return BriefWait(30);
            }

            var job = JobMaker.MakeJob(JobDefOf.Goto, anchor);
            job.expiryInterval = 60;
            job.checkOverrideOnExpire = true;
            job.locomotionUrgency = LocomotionUrgency.Jog;
            return job;
        }

        private static Job BriefWait(int ticks)
        {
            var wait = JobMaker.MakeJob(JobDefOf.Wait_Combat);
            wait.expiryInterval = ticks;
            wait.checkOverrideOnExpire = true;
            return wait;
        }
    }

    /// <summary>
    /// If its caller is gone, the central aperture drifts only a couple of
    /// cells at a time and waits for the submerged anatomy to settle before
    /// moving again. This replaces a vanilla indefinite wander job.
    /// </summary>
    public class JobGiver_LeviathanHeadIdle : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            var core = pawn.TryGetComp<CompLeviathanCore>();
            if (core == null)
                return null;
            if (!core.FormationReadyToAdvance())
                return BriefWait();

            var destination =
                CellFinder.RandomClosewalkCellNear(pawn.Position, pawn.Map, 2);
            if (!destination.IsValid || destination == pawn.Position)
                return BriefWait();

            var drift = JobMaker.MakeJob(JobDefOf.Goto, destination);
            drift.expiryInterval = 60;
            drift.checkOverrideOnExpire = true;
            drift.locomotionUrgency = LocomotionUrgency.Walk;
            return drift;
        }

        private static Job BriefWait()
        {
            var wait = JobMaker.MakeJob(JobDefOf.Wait_Combat);
            wait.expiryInterval = 45;
            wait.checkOverrideOnExpire = true;
            return wait;
        }
    }
}
