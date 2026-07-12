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
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra() =>
            Active ? base.CompGetGizmosExtra() : System.Linq.Enumerable.Empty<Gizmo>();

        public override void PostDraw()
        {
            if (Active)
                base.PostDraw();
        }
    }

    /// Applies the Tideguard shield hediff to a freshly summoned tentacle
    /// if its caster has learned the passive; severity tracks growth stage
    /// (CompTentacleGrowth keeps it in sync as the tentacle evolves).
    public static class TideguardUtility
    {
        public static void TryApply(Pawn tentacle, Pawn caster)
        {
            if (caster?.GetComp<VEF.Abilities.CompAbilities>()?.LearnedAbilities
                    ?.Any(a => a.def == Deepcaller_DefOf.Deepcaller_Tideguard) != true)
                return;
            var growth = tentacle.TryGetComp<CompTentacleGrowth>();
            if (growth == null)
                return;
            var shield = tentacle.health.hediffSet.GetFirstHediffOfDef(Deepcaller_DefOf.Deepcaller_TideguardShield)
                         ?? tentacle.health.AddHediff(Deepcaller_DefOf.Deepcaller_TideguardShield);
            shield.Severity = growth.Stage + 1;
        }
    }
}
