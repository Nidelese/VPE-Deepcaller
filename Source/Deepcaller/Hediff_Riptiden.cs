using RimWorld;
using Verse;

namespace Deepcaller
{
    /// The current itself: applied by Riptide, this drags the victim one
    /// cell toward the anchor every ticksPerCell until it reaches the ring,
    /// then stuns it and ends. Hitting a wall (or an impassable cell) slams
    /// the victim into it — blunt damage to both victim and edifice — and
    /// landing on another pawn bruises both. The Disappears comp on the def
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

        private int ticksUntilStep;

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
            if (!pawn.Spawned || !anchor.IsValid)
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
            if (!next.IsValid)
            {
                End();
                return;
            }

            if (!next.Standable(pawn.Map))
            {
                Slam(next);
                return;
            }

            FleckMaker.ThrowDustPuffThick(pawn.DrawPos, pawn.Map, 1.2f, DeepPullUtility.DeepColor);
            pawn.Position = next;
            pawn.Notify_Teleported(endCurrentJob: true, resetTweenedPos: true);
        }

        /// Dragged into a wall: blunt damage to the victim, and the wall
        /// takes its own devotion-scaled hit. The deep does not care whose
        /// masonry it is. Damage is queued, not applied — we are inside the
        /// victim's own health tick here, and a kill from this stack can
        /// silently lose the corpse.
        private void Slam(IntVec3 cell)
        {
            var map = pawn.Map;
            var edifice = cell.GetEdifice(map);
            FleckMaker.ThrowDustPuffThick(cell.ToVector3Shifted(), map, 2f, DeepPullUtility.DeepColor);
            DeepcallerGameComponent.QueueDamage(pawn, collisionDamage, caster);
            if (edifice != null)
                DeepcallerGameComponent.QueueDamage(edifice, wallDamage, caster);
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
                    if (things[i] is Pawn other && other != pawn && !other.Dead)
                    {
                        DeepcallerGameComponent.QueueDamage(other, collisionDamage, caster);
                        DeepcallerGameComponent.QueueDamage(pawn, collisionDamage, caster);
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
            Scribe_Values.Look(ref ticksUntilStep, "ticksUntilStep");
        }
    }
}
