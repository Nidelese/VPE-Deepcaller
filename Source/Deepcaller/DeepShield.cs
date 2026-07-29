using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Deepcaller
{
    /// Vanilla shield-belt comp, mounted on the tentacle race but inert
    /// until the Tideguard hediff is present. Gating on the hediff (not the
    /// stat) avoids EnergyShieldEnergyMax's non-zero defaultBaseValue making
    /// Active always true. CompTick is also gated so the vanilla reset timer
    /// never hands the shield a burst of energy when the passive is unlearned.
    public class CompShield_Deep : CompShield
    {
        private int lastCustomAbsorbTick = -9999;

        private bool Active =>
            ((Pawn)parent).health.hediffSet.HasHediff(Deepcaller_DefOf.Deepcaller_TideguardShield);

        public override void CompTick()
        {
            if (!Active)
                return;
            base.CompTick();
        }

        public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            if (!Active)
            {
                absorbed = false;
                return;
            }
            base.PostPreApplyDamage(ref dinfo, out absorbed);
            if (!absorbed)
                return;

            lastCustomAbsorbTick = Find.TickManager.TicksGame;
            if (ShieldState == ShieldState.Resetting && parent.Spawned)
            {
                var shield = ((Pawn)parent).health.hediffSet
                    .GetFirstHediffOfDef(Deepcaller_DefOf.Deepcaller_TideguardShield);
                Thing_AbilityVisual.SpawnShieldBreak(
                    parent.Map, parent.Position, ShieldScale(shield?.Severity ?? 1f));
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra() =>
            Active ? base.CompGetGizmosExtra() : System.Linq.Enumerable.Empty<Gizmo>();

        public override void PostDraw()
        {
            if (!Active || ShieldState != ShieldState.Active)
                return;

            var pawn = (Pawn)parent;
            var shield = pawn.health.hediffSet
                .GetFirstHediffOfDef(Deepcaller_DefOf.Deepcaller_TideguardShield);
            var energyMax = parent.GetStatValue(StatDefOf.EnergyShieldEnergyMax);
            var energyPercent = energyMax > 0f ? Energy / energyMax : 0f;
            DeepcallerVisualRenderer.DrawTideguard(
                pawn, shield?.Severity ?? 1f, energyPercent,
                Find.TickManager.TicksGame - lastCustomAbsorbTick);
        }

        private static float ShieldScale(float severity) =>
            1.32f + (UnityEngine.Mathf.Clamp(severity, 1f, 4f) - 1f) * 0.12f;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(
                ref lastCustomAbsorbTick, "deepcallerLastShieldImpact", -9999);
        }
    }

    /// Applies the Tideguard shield hediff to a freshly summoned tentacle
    /// if its caster has learned the passive; severity tracks growth stage
    /// where one exists, while static Buds receive the stage-one shell.
    public static class TideguardUtility
    {
        public static void TryApply(Pawn tentacle, Pawn caster)
        {
            if (caster?.GetComp<VEF.Abilities.CompAbilities>()?.LearnedAbilities
                    ?.Any(a => a.def == Deepcaller_DefOf.Deepcaller_Tideguard) != true)
                return;
            var shield = tentacle.health.hediffSet.GetFirstHediffOfDef(Deepcaller_DefOf.Deepcaller_TideguardShield)
                         ?? tentacle.health.AddHediff(Deepcaller_DefOf.Deepcaller_TideguardShield);
            var stage = tentacle.def.defName == "Deepcaller_LeviathanArm"
                ? 2
                : tentacle.TryGetComp<CompTentacleGrowth>()?.Stage ?? 0;
            shield.Severity = stage + 1;
        }
    }
}
