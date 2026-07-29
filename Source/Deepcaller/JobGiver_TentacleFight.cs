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
            var part = pawn.TryGetComp<CompLeviathanPart>();

            // A Leviathan arm is not a free animal with a loose leash. It is
            // an aperture into one submerged body: choose prey relative to the
            // head, reach only through this arm's sector, and reconsider the
            // extension almost immediately.
            if (part?.IsLinked == true && !part.IsCore)
                return part.IsFlower ? null : CohesiveArmAttack(pawn, part);

            var core = pawn.TryGetComp<CompLeviathanCore>();
            if (core != null)
            {
                core.UpdateSharedThreat();
                var command = core.SharedThreat;
                pawn.mindState.enemyTarget = command;
                if (command == null)
                    return null;
                if (!core.FormationReadyToAdvance())
                    return BriefBraceJob();
                return CohesiveHeadAdvance(command);
            }

            // Think nodes are shared between pawns; setting the radii on every
            // invocation (rather than caching) keeps them per-pawn correct.
            var growth = pawn.TryGetComp<CompTentacleGrowth>();
            if (growth != null)
            {
                targetAcquireRadius = growth.AcquireRadius;
                targetKeepRadius = growth.AcquireRadius + 8f;
            }

            var job = base.TryGiveJob(pawn);
            if (job != null)
            {
                if (core != null)
                {
                    // The central body advances in deliberate short steps,
                    // then gives its apertures a chance to settle around it.
                    job.expiryInterval = 45;
                    job.checkOverrideOnExpire = true;
                }
                return job;
            }

            var downed = FindDownedHostile(pawn, targetAcquireRadius);
            if (downed == null)
                return null;

            var execute = JobMaker.MakeJob(JobDefOf.AttackMelee, downed);
            execute.killIncappedTarget = true;
            execute.expiryInterval = 600;
            return execute;
        }

        private static Job CohesiveArmAttack(
            Pawn pawn,
            CompLeviathanPart part)
        {
            part.Core?.UpdateSharedThreat();
            if (part.Core?.SharedThreat == null)
            {
                pawn.mindState.enemyTarget = null;
                return null;
            }

            var target = FindCohesiveTarget(pawn, part);
            if (target == null)
            {
                pawn.mindState.enemyTarget = null;
                return null;
            }

            pawn.mindState.enemyTarget = target;
            var attack = JobMaker.MakeJob(JobDefOf.AttackMelee, target);
            attack.killIncappedTarget = target is Pawn victim && victim.Downed;
            // Long enough to traverse the arm's roughly two-cell extension
            // and complete the custom curl/snap anticipation. The per-tick
            // anatomical fail condition still cancels escaped prey instantly.
            attack.expiryInterval = 90;
            attack.checkOverrideOnExpire = true;
            attack.expireRequiresEnemiesNearby = true;
            return attack;
        }

        private static Thing FindCohesiveTarget(
            Pawn pawn,
            CompLeviathanPart part)
        {
            Thing best = null;
            float bestScore = float.MaxValue;
            foreach (var candidate in
                     pawn.Map.attackTargetsCache.GetPotentialTargetsFor(pawn))
            {
                var target = candidate.Thing;
                var isDownedPawn =
                    target is Pawn candidatePawn && candidatePawn.Downed;
                if (target == null || target.Destroyed || !target.Spawned
                    || !target.HostileTo(pawn)
                    || (!isDownedPawn && candidate.ThreatDisabled(pawn))
                    || part.Core?.IsInCommandFront(target) != true
                    || !part.CanPredate(target)
                    || !pawn.CanReach(
                        target, PathEndMode.Touch, Danger.Deadly))
                    continue;

                float score =
                    target.Position.DistanceToSquared(pawn.Position);
                score += target.Position.DistanceToSquared(
                             part.Core.SharedThreat.Position) * 0.25f;
                if (isDownedPawn)
                    score += 30f;

                // Arms choose independently, but a soft claim penalty lets a
                // crowd of prey produce an octopus-like spread instead of all
                // eight arms pathing through one another toward one pawn.
                score += part.Core?.PredationClaims(target, pawn) * 12f ?? 0f;

                if (score < bestScore)
                {
                    best = target;
                    bestScore = score;
                }
            }
            return best;
        }

        private static Job CohesiveHeadAdvance(Thing command)
        {
            var advance = JobMaker.MakeJob(JobDefOf.AttackMelee, command);
            advance.expiryInterval = 45;
            advance.checkOverrideOnExpire = true;
            advance.expireRequiresEnemiesNearby = true;
            return advance;
        }

        private static Job BriefBraceJob()
        {
            var wait = JobMaker.MakeJob(JobDefOf.Wait_Combat);
            wait.expiryInterval = 30;
            wait.checkOverrideOnExpire = true;
            return wait;
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
