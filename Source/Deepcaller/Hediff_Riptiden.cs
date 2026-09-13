using RimWorld;
using Verse;

namespace Deepcaller
{
    /// The current itself: applied by Riptide, this drags the victim one
    /// cell toward the anchor every ticksPerCell until it reaches the ring,
    /// then stuns it and ends. Its body mines rock and smashes structures;
    /// a protected or surviving obstruction slams the victim. Landing on another
    /// hostile pawn bruises both. The Disappears comp on the def
    /// is a failsafe so a stuck drag never lingers.
    public class Hediff_Riptiden : HediffWithComps
    {
        public IntVec3 anchor = IntVec3.Invalid;
        public Pawn caster;
        public int ticksPerCell = 5;
        public float ringWidth = 1.4f;
        public int stunTicks = 120;
        public float collisionDamage;
        public float wallDamage;
        private double cellsMoved;

        private int ticksUntilStep;
        private Thing struckObstacle;
        private bool impactApplied;
        private bool impactQueued;

        public void ResetMomentum()
        {
            cellsMoved = 0;
            ResetObstruction();
        }

        public void ResetObstruction()
        {
            struckObstacle = null;
            impactApplied = false;
            impactQueued = false;
            ticksUntilStep = ticksPerCell;
        }

        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            ticksUntilStep = ticksPerCell;
            pawn.pather?.StopDead();
            pawn.jobs?.StopAll();
        }

        public override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            if (!RiptideImpactUtility.AffectsPawn(pawn, caster) || Severity <= 0 || !anchor.IsValid || !anchor.InBounds(pawn.Map))
            {
                Severity = 0f;
                return;
            }

            ticksUntilStep -= delta;
            if (ticksUntilStep > 0)
                return;
            ticksUntilStep = ticksPerCell;

            if (pawn.Position.InHorDistOf(anchor, ringWidth))
            {
                Arrive();
                return;
            }

            // One cell along the sight line toward the anchor.
            var next = IntVec3.Invalid;
            foreach (var cell in GenSight.PointsOnLineOfSight(pawn.Position, anchor))
            {
                if (cell == pawn.Position)
                    continue;
                next = cell;
                break;
            }
            if (!next.IsValid || !next.InBounds(pawn.Map))
            {
                End();
                return;
            }

            if (!next.Standable(pawn.Map))
            {
                var obstacle = next.GetEdifice(pawn.Map);
                if (obstacle != struckObstacle)
                {
                    struckObstacle = obstacle;
                    impactApplied = false;
                    impactQueued = false;
                }
                if (!impactApplied && (impactQueued || DeepcallerGameComponent.QueueRiptideImpact(
                        obstacle, RiptideMath.MomentumDamage(wallDamage, cellsMoved), caster, this)))
                {
                    // One body impact per obstacle. Wait for damage to land
                    // outside the health tick, then pass through if it broke.
                    impactQueued = true;
                    ticksUntilStep = 1;
                    return;
                }
                Slam(next);
                return;
            }

            cellsMoved += pawn.Position.DistanceTo(next);
            pawn.Position = next;
            ResetObstruction();
            pawn.Notify_Teleported(endCurrentJob: true, resetTweenedPos: true);
        }

        public void ResolveObstacleImpact(Thing obstacle, float pressure)
        {
            if (obstacle != struckObstacle) return;
            impactQueued = false;
            impactApplied = true;
            if (Severity <= 0 || !pawn.Spawned || pawn.Dead || !pawn.health.hediffSet.hediffs.Contains(this)) return;
            RiptideImpactUtility.Apply(pawn, obstacle, pressure, caster);
        }

        /// A protected or surviving obstruction is a hard stop and hurts the
        /// victim. Damage is queued, not applied — we are inside the
        /// victim's own health tick here, and a kill from this stack can
        /// silently lose the corpse.
        private void Slam(IntVec3 cell)
        {
            var map = pawn.Map;
            FleckMaker.ThrowDustPuffThick(cell.ToVector3Shifted(), map, 2f, DeepPullUtility.DeepColor);
            DeepcallerGameComponent.QueueRiptideDamage(pawn, RiptideMath.MomentumDamage(collisionDamage, cellsMoved), caster);
            End();
        }

        /// Reached the ring: bruise anything already standing there, stun.
        /// Damage queued for the same reason as Slam.
        private void Arrive()
        {
            if (collisionDamage > 0f)
            {
                var things = pawn.Position.GetThingList(pawn.Map);
                for (var i = things.Count - 1; i >= 0; i--)
                {
                    if (things[i] is Pawn other && other != pawn && RiptideImpactUtility.AffectsPawn(other, caster))
                    {
                        float impactDamage = RiptideMath.MomentumDamage(collisionDamage, cellsMoved);
                        DeepcallerGameComponent.QueueRiptideDamage(other, impactDamage, caster);
                        DeepcallerGameComponent.QueueRiptideDamage(pawn, impactDamage, caster);
                        break;
                    }
                }
            }
            End();
        }

        private void End()
        {
            if (!pawn.Dead && pawn.Spawned)
                pawn.stances?.stunner?.StunFor(stunTicks, caster, addBattleLog: false);
            Severity = 0f;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref anchor, "anchor", IntVec3.Invalid);
            Scribe_References.Look(ref caster, "caster");
            Scribe_Values.Look(ref ticksPerCell, "ticksPerCell", 5);
            Scribe_Values.Look(ref ringWidth, "ringWidth", 1.4f);
            Scribe_Values.Look(ref stunTicks, "stunTicks", 120);
            Scribe_Values.Look(ref collisionDamage, "collisionDamage");
            Scribe_Values.Look(ref wallDamage, "wallDamage");
            Scribe_Values.Look(ref cellsMoved, "riptideCellsMoved", 0.0);
            Scribe_Values.Look(ref ticksUntilStep, "ticksUntilStep");
            // Keep the preview save key. A queued-but-unapplied impact is
            // retried after load; a completed hit cannot be applied twice.
            Scribe_References.Look(ref struckObstacle, "struckRock");
            Scribe_Values.Look(ref impactApplied, "riptideImpactApplied");
        }
    }
}
