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

        // The curve covers the early/mid game. Beyond its last point, each
        // doubling of devotion adds two cells (bounded for map performance).
        public SimpleCurve radiusByDevotion;

        // Body impacts mine natural rock and smash constructed obstacles.
        // Keep the old field names for XML/save compatibility. Sparing our
        // structures requires purchased restraint and a sated local idol.
        public float wallDamageBase = 0f;
        public float wallDamagePerDevotion = 1f;

        // Long enough to accompany a maximum-range drag, with a closing
        // ring after the surge has done its work.
        public int visualDurationTicks = 120;
    }

    /// The deep's calamity: capture loose bodies at cast time, then drag
    /// them toward the center. Momentum and mass turn them into impacts.
    public class Ability_Riptide : VEF.Abilities.Ability
    {
        private static readonly AbilityExtension_Riptide DefaultExt = new AbilityExtension_Riptide();

        private AbilityExtension_Riptide Ext =>
            def.GetModExtension<AbilityExtension_Riptide>() ?? DefaultExt;

        private double Devotion => RiptideMath.Devotion(CompIdolDevotion.BestIdol(pawn.MapHeld, pawn.Faction)?.TotalDevotion ?? 0);

        private float BonusCells =>
            (float)System.Math.Min(System.Math.Floor(Devotion / System.Math.Max(1, Ext.devotionPerBonusCell)), Ext.maxBonusCells);

        private double MapExtent => pawn.MapHeld?.Size.LengthHorizontal ?? 1000;

        public override float GetRangeForPawn() => RiptideMath.MapBoundedReach(base.GetRangeForPawn() + BonusCells,
            RiptideMath.ReachExtension(Devotion, Cultivation.Rank(pawn, BudUpgradeKind.RiptideReach), false), MapExtent);

        public override float GetRadiusForPawn()
        {
            var curve = Ext.radiusByDevotion;
            float baseline;
            if (curve != null && curve.Points.Any())
            {
                var last = curve.Points.Last();
                baseline = RiptideMath.ExtendedRadius(Devotion, curve.Evaluate((float)System.Math.Min(Devotion, float.MaxValue)), last.x, last.y);
            }
            else baseline = Mathf.Min(RiptideMath.MaximumRadius, base.GetRadiusForPawn() + BonusCells);
            return RiptideMath.MapBoundedReach(baseline,
                RiptideMath.ReachExtension(Devotion, Cultivation.Rank(pawn, BudUpgradeKind.RiptideArea), true), MapExtent);
        }

        public override string GetDescriptionForPawn() => base.GetDescriptionForPawn() + "\n\n"
            + "Deepcaller_RiptideForce".Translate(Cultivation.Number(Devotion),
                PreviewImpact(0, true), PreviewImpact(3, true), PreviewImpact(7, true), PreviewImpact(3, false),
                GetRadiusForPawn().ToString("0.#")) + "\n"
            + (RiptideImpactUtility.ProtectsStructures(pawn)
                ? "Deepcaller_RiptideRestraintActive" : "Deepcaller_RiptideRestraintInactive").Translate() + "\n"
            + (RiptideImpactUtility.TargetsHostilesOnly(pawn)
                ? "Deepcaller_RiptideDiscernmentActive" : "Deepcaller_RiptideDiscernmentInactive").Translate() + "\n"
            + (RiptideImpactUtility.ProtectsPossessions(pawn)
                ? "Deepcaller_RiptidePossessionsActive" : "Deepcaller_RiptidePossessionsInactive").Translate();

        private string PreviewImpact(double distance, bool structure)
        {
            float pressure = RiptideMath.Pressure(Devotion,
                structure ? Ext.wallDamageBase : Ext.collisionDamageBase,
                structure ? Ext.wallDamagePerDevotion : Ext.collisionDamagePerDevotion);
            double force = Cultivation.Factor(pawn, BudUpgradeKind.RiptideAcceleration, 0.1f);
            double acceleration = RiptideKinetics.Acceleration(Devotion, 70, force, Ext.ticksPerCell);
            double speed = System.Math.Min(RiptideKinetics.MaximumSpeed, System.Math.Sqrt(2 * acceleration * distance));
            return Cultivation.Number(RiptideKinetics.Damage(pressure, 70, speed));
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

        public override void DrawHighlight(LocalTargetInfo target)
        {
            base.DrawHighlight(target);
            // VEF omits rings larger than the game's precomputed radial grid.
            // These bounded-segment outlines do not index that grid.
            if (GetRangeForPawn() >= GenRadial.MaxRadialPatternRadius)
                GenDraw.DrawCircleOutline(pawn.Position.ToVector3ShiftedWithAltitude(AltitudeLayer.MetaOverlays),
                    GetRangeForPawn(), SimpleColor.White);
            if (target.IsValid && GetRadiusForPawn() >= GenRadial.MaxRadialPatternRadius)
                GenDraw.DrawCircleOutline(target.Cell.ToVector3ShiftedWithAltitude(AltitudeLayer.MetaOverlays),
                    GetRadiusForPawn(), SimpleColor.Cyan);
        }

        public override void Cast(params GlobalTargetInfo[] targets)
        {
            var ext = Ext;
            var map = pawn.Map;
            var devotion = Devotion;
            var radius = GetRadiusForPawn();
            var collisionSnapshot = map.listerThings.AllThings.Where(t =>
                RiptideImpactUtility.IsMovable(t) || t is Building
                    || t is Plant && t.def.fillPercent > 0.2f).ToList();
            // Snapshot before casting or applying any effects. Dead bodies,
            // gear and vehicle wreckage created afterward never enter a roster.
            var snapshots = targets.Where(t => t.Cell.InBounds(map)).Select(t => new
            {
                center = t.Cell,
                bodies = map.listerThings.AllThings.Where(thing => RiptideImpactUtility.IsMovable(thing)
                    && thing.Position.InHorDistOf(t.Cell, radius)).ToList()
            }).ToList();
            base.Cast(targets);
            foreach (var snapshot in snapshots)
            {
                Thing_AbilityVisual.SpawnRiptide(
                    map, snapshot.center, radius, ext.visualDurationTicks);
                Thing_RiptideSurge.Spawn(map, snapshot.center, pawn, devotion, ext, radius, snapshot.bodies, collisionSnapshot);
            }
        }
    }
}
