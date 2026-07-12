using RimWorld;
using UnityEngine;
using Verse;

namespace Deepcaller
{
    public class GraspExtension : DefModExtension
    {
        // Severity lost per point of damage the victim takes. At initial
        // severity 1.0, 0.03 means ~33 damage tears the victim free early.
        // Divided by gripStrength, which Ability_Grasp raises with devotion.
        public float severityPerDamage = 0.03f;

        // Idol devotion scaling, read by Ability_Grasp at cast time:
        // duration ×(1 + factor×devotion), gripStrength 1 + per×devotion.
        public float durationFactorPerDevotion = 0.35f;
        public float gripStrengthPerDevotion = 0.5f;

        // The leash: while the victim is farther than leashRadius from the
        // eruption point, every pullIntervalTicks it is dragged up to
        // pullDistance cells back along the line toward the anchor.
        public int pullIntervalTicks = 60;
        public float leashRadius = 1.9f;
        public int pullDistance = 2;
    }

    /// A tentacle erupts under the victim and leashes it to that spot. The
    /// victim is slowed but never downed (downing breaks raid morale and
    /// makes survivors flee on waking) — instead, straying from the anchor
    /// gets it periodically dragged back. Holds until the duration expires
    /// (HediffComp_Disappears, set from the ability's durationTime) or until
    /// enough damage breaks the grip — including damage dealt by your own
    /// tentacles pounding on the held target.
    public class Hediff_Grasped : HediffWithComps
    {
        private static readonly Color DeepColor = new Color(0.35f, 0.55f, 0.5f);
        private static readonly GraspExtension DefaultExt = new GraspExtension();

        private IntVec3 anchor = IntVec3.Invalid;
        private int ticksUntilPull;

        // Set by Ability_Grasp from Idol devotion; divides break-on-damage.
        public float gripStrength = 1f;

        private GraspExtension Ext => def.GetModExtension<GraspExtension>() ?? DefaultExt;

        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            if (!pawn.Spawned)
                return;
            anchor = pawn.Position;
            FilthMaker.TryMakeFilth(pawn.Position, pawn.Map, ThingDefOf.Filth_Slime);
            for (var i = 0; i < 5; i++)
                FleckMaker.ThrowDustPuffThick(pawn.DrawPos, pawn.Map, 2f, DeepColor);
        }

        public override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            if (!pawn.Spawned || pawn.Downed)
                return;

            ticksUntilPull -= delta;
            if (ticksUntilPull > 0)
                return;
            ticksUntilPull = Ext.pullIntervalTicks;

            if (!anchor.IsValid)
                anchor = pawn.Position;
            if (pawn.Position.InHorDistOf(anchor, Ext.leashRadius))
                return;

            DeepPullUtility.PullTowards(pawn, anchor, Ext.pullDistance, Ext.leashRadius);
        }

        public override void Notify_PawnPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.Notify_PawnPostApplyDamage(dinfo, totalDamageDealt);
            Severity -= totalDamageDealt * Ext.severityPerDamage / Mathf.Max(gripStrength, 0.01f);
        }

        public override void PostRemoved()
        {
            base.PostRemoved();
            if (pawn.Spawned && !pawn.Dead)
                MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "Deepcaller_GraspReleased".Translate(), 2.5f);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref anchor, "anchor", IntVec3.Invalid);
            Scribe_Values.Look(ref ticksUntilPull, "ticksUntilPull");
            Scribe_Values.Look(ref gripStrength, "gripStrength", 1f);
        }
    }
}
