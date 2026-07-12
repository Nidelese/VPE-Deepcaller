using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Deepcaller
{
    public class AbilityExtension_Riptide : DefModExtension
    {
        // Pull speed and landing.
        public int ticksPerCell = 5;
        public int stunTicks = 120;
        public float ringWidth = 1.4f;

        // Devotion economy: the deep must be fed to surge at all, and a
        // better-fed deep reaches farther and hits harder.
        public float minDevotion = 10f;
        public float devotionPerBonusCell = 5f;   // +1 cast range per this much devotion
        public float maxBonusCells = 8f;
        public float collisionDamageBase = 3f;
        public float collisionDamagePerDevotion = 0.4f;

        // AoE radius comes entirely from this curve when present (def.radius
        // is ignored): diminishing returns, always growing, per Evelyn's
        // spec. Below the first point it clamps to the first point's value.
        public SimpleCurve radiusByDevotion;

        // Walls take their own scaling, separate from pawn collision damage:
        // ~1 damage per devotion means 200 devotion breaks a 200 hp wooden
        // wall in one slam. Set both to 0 to spare masonry entirely.
        public float wallDamageBase = 0f;
        public float wallDamagePerDevotion = 1f;
    }

    /// The deep's current: every hostile in the radius is dragged cell by
    /// cell toward the target point (Hediff_Riptiden does the dragging),
    /// slamming into walls and each other, then stunned. Requires a fed
    /// idol; range, radius and collision damage scale with devotion.
    public class Ability_Riptide : VEF.Abilities.Ability
    {
        private static readonly AbilityExtension_Riptide DefaultExt = new AbilityExtension_Riptide();

        private AbilityExtension_Riptide Ext =>
            def.GetModExtension<AbilityExtension_Riptide>() ?? DefaultExt;

        private float Devotion => CompIdolDevotion.HighestDevotion(pawn.MapHeld, pawn.Faction);

        private float BonusCells =>
            Mathf.Min(Mathf.Floor(Devotion / Ext.devotionPerBonusCell), Ext.maxBonusCells);

        public override float GetRangeForPawn() => base.GetRangeForPawn() + BonusCells;

        public override float GetRadiusForPawn()
        {
            var curve = Ext.radiusByDevotion;
            if (curve != null)
                return curve.Evaluate(Devotion);
            return base.GetRadiusForPawn() + BonusCells;
        }

        public override bool IsEnabledForPawn(out string reason)
        {
            var devotion = Devotion;
            if (devotion < Ext.minDevotion)
            {
                reason = "Deepcaller_RiptideNeedsDevotion".Translate(
                    Ext.minDevotion.ToString("0.#"), devotion.ToString("0.#"));
                return false;
            }
            return base.IsEnabledForPawn(out reason);
        }

        public override void Cast(params GlobalTargetInfo[] targets)
        {
            base.Cast(targets);
            var ext = Ext;
            var map = pawn.Map;
            var devotion = Devotion;
            var radius = GetRadiusForPawn();
            var collisionDamage = ext.collisionDamageBase + ext.collisionDamagePerDevotion * devotion;
            var wallDamage = ext.wallDamageBase + ext.wallDamagePerDevotion * devotion;

            foreach (var target in targets)
            {
                var center = target.Cell;
                for (var i = 0; i < 8; i++)
                    FleckMaker.ThrowDustPuffThick(center.ToVector3Shifted(), map, 2.5f, DeepPullUtility.DeepColor);

                foreach (var victim in map.mapPawns.AllPawnsSpawned
                             .Where(p => p.HostileTo(pawn) && p.Position.InHorDistOf(center, radius))
                             .ToList())
                {
                    // Already in the ring: caught by the surge, no drag needed.
                    if (victim.Position.InHorDistOf(center, ext.ringWidth))
                    {
                        victim.stances?.stunner?.StunFor(ext.stunTicks, pawn, addBattleLog: false);
                        continue;
                    }

                    // Re-cast on someone mid-drag just redirects the current.
                    if (victim.health.hediffSet.GetFirstHediffOfDef(Deepcaller_DefOf.Deepcaller_Riptiden)
                        is Hediff_Riptiden existing)
                    {
                        existing.anchor = center;
                        existing.collisionDamage = collisionDamage;
                        existing.wallDamage = wallDamage;
                        continue;
                    }

                    var drag = (Hediff_Riptiden)HediffMaker.MakeHediff(Deepcaller_DefOf.Deepcaller_Riptiden, victim);
                    drag.anchor = center;
                    drag.caster = pawn;
                    drag.ticksPerCell = ext.ticksPerCell;
                    drag.ringWidth = ext.ringWidth;
                    drag.stunTicks = ext.stunTicks;
                    drag.collisionDamage = collisionDamage;
                    drag.wallDamage = wallDamage;
                    victim.health.AddHediff(drag);
                }
            }
        }
    }
}
