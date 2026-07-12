using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Deepcaller
{
    public class AbilityExtension_Consume : DefModExtension
    {
        // Neural heat gained per point of the victim's remaining body part
        // hit points. ~0.2 puts a healthy human around 40-60 heat: lethal
        // arrogance for a novice, an afternoon snack for an archon.
        // Wounding the victim first makes it cheaper to swallow.
        public float heatPerHitPoint = 0.2f;

        // Devotion per victim body size. Corpse offerings pay 1x via the
        // idol; the deep prefers them wriggling.
        public float liveOfferingFactor = 1.5f;
    }

    /// The deep swallows a living creature whole — instant death, no
    /// corpse, devotion fed to the idol from anywhere on the map. The
    /// victim's vitality passes through the caster as neural heat on its
    /// way down; devour something beyond your capacity and the idol eats
    /// you too. Any living creature is a valid offering. The deep does not
    /// judge.
    public class Ability_Consume : VEF.Abilities.Ability
    {
        private static readonly AbilityExtension_Consume DefaultExt = new AbilityExtension_Consume();

        private AbilityExtension_Consume Ext =>
            def.GetModExtension<AbilityExtension_Consume>() ?? DefaultExt;

        public override bool IsEnabledForPawn(out string reason)
        {
            if (!CompIdolDevotion.AnyIdol(pawn.MapHeld, pawn.Faction))
            {
                reason = "Deepcaller_ConsumeNoIdol".Translate();
                return false;
            }
            return base.IsEnabledForPawn(out reason);
        }

        public override bool ValidateTarget(LocalTargetInfo target, bool showMessages = true)
        {
            if (!(target.Thing is Pawn victim) || victim.Dead || !victim.RaceProps.IsFlesh || victim == pawn)
            {
                if (showMessages)
                    Messages.Message("Deepcaller_ConsumeInvalidTarget".Translate(),
                        target.Thing, MessageTypeDefOf.RejectInput, historical: false);
                return false;
            }
            return base.ValidateTarget(target, showMessages);
        }

        public override void Cast(params GlobalTargetInfo[] targets)
        {
            base.Cast(targets);
            var ext = Ext;
            foreach (var target in targets)
            {
                if (!(target.Thing is Pawn victim) || victim.Dead)
                    continue;

                var heat = RemainingHitPoints(victim) * ext.heatPerHitPoint;
                var overflow = pawn.psychicEntropy.WouldOverflowEntropy(heat);
                if (!overflow)
                    pawn.psychicEntropy.TryAddEntropy(heat, pawn, scale: false);

                Devour(victim, victim.BodySize * ext.liveOfferingFactor);

                if (overflow)
                {
                    // The deep does not distinguish offering from officiant.
                    Messages.Message("Deepcaller_CasterConsumed".Translate(pawn.Named("CASTER")),
                        new LookTargets(pawn.Position, pawn.Map), MessageTypeDefOf.NegativeHealthEvent);
                    pawn.Strip();
                    Devour(pawn, pawn.BodySize * ext.liveOfferingFactor);
                }
            }
        }

        /// Instant death, no corpse; the flesh becomes devotion. With the
        /// Deep Hoard passive learned, the indigestible bits — equipment,
        /// apparel, inventory — survive into the idol's belly for ransom.
        private void Devour(Pawn victim, float bodySizeEquivalent)
        {
            if (HasDeepHoard)
                StashGear(victim, CompIdolDevotion.BestIdol(victim.MapHeld ?? pawn.Map, pawn.Faction));
            DeepDevourUtility.Devour(victim, pawn.Faction, bodySizeEquivalent);
        }

        private bool HasDeepHoard =>
            pawn.GetComp<VEF.Abilities.CompAbilities>()?.LearnedAbilities
                ?.Any(a => a.def == Deepcaller_DefOf.Deepcaller_DeepHoard) ?? false;

        private static void StashGear(Pawn victim, CompIdolDevotion idol)
        {
            if (idol == null)
                return;
            if (victim.equipment != null)
                foreach (var eq in victim.equipment.AllEquipmentListForReading.ToList())
                    eq.holdingOwner.TryTransferToContainer(eq, idol.Hoard);
            if (victim.apparel != null)
                foreach (var ap in victim.apparel.WornApparel.ToList())
                    ap.holdingOwner.TryTransferToContainer(ap, idol.Hoard);
            if (victim.inventory != null)
                foreach (var item in victim.inventory.innerContainer.ToList())
                    item.holdingOwner.TryTransferToContainer(item, idol.Hoard);
        }

        /// Current (not max) part hit points: soften a big victim first and
        /// it goes down easier.
        private static float RemainingHitPoints(Pawn victim)
        {
            var total = 0f;
            foreach (var part in victim.health.hediffSet.GetNotMissingParts())
                total += part.def.GetMaxHealth(victim);
            total -= victim.health.hediffSet.hediffs.OfType<Hediff_Injury>().Sum(h => h.Severity);
            return Mathf.Max(total, 1f);
        }
    }
}
