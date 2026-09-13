using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Deepcaller
{
    public class AbilityExtension_Consume : DefModExtension
    {
        // Vitality is one part of the price; wealth and combat power also
        // travel through the caster. Wounding prey helps without erasing worth.
        public float heatPerHitPoint = 0.4f;

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

        private double Devotion => CompIdolDevotion.BestIdol(pawn.MapHeld, pawn.Faction)?.TotalDevotion ?? 0;
        public override float GetRadiusForPawn() => (float)System.Math.Min(pawn.MapHeld?.Size.LengthHorizontal ?? 350,
            Cultivation.Rank(pawn, BudUpgradeKind.ConsumeArea));

        public double HeatFor(Pawn victim, double devotion, int burnRank, float radius) => ConsumeMath.Heat(
            ConsumeMath.RawHeat(RemainingHitPoints(victim), ConsumeWorth.MarketValue(victim), victim.kindDef.combatPower,
                victim.GetStatValue(StatDefOf.MeleeDamageFactor), ConsumeWorth.Level(victim), Ext.heatPerHitPoint),
            devotion, burnRank, radius);

        public override string GetDescriptionForPawn() => base.GetDescriptionForPawn() + "\n\n"
            + "Deepcaller_ConsumeCostInfo".Translate(GetRadiusForPawn().ToString("0"),
                Cultivation.Number(Devotion), Cultivation.Factor(pawn, BudUpgradeKind.ConsumeBurn, 0.2f).ToString("0.##"));

        public override void DrawHighlight(LocalTargetInfo target)
        {
            base.DrawHighlight(target);
            if (target.IsValid && GetRadiusForPawn() >= GenRadial.MaxRadialPatternRadius)
                GenDraw.DrawCircleOutline(target.Cell.ToVector3ShiftedWithAltitude(AltitudeLayer.MetaOverlays),
                    GetRadiusForPawn(), SimpleColor.Red);
        }

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
            if (!(target.Thing is Pawn victim) || victim.Dead || !DeepTargetUtility.IsEdible(victim) || victim == pawn)
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
            if (pawn.Dead || !pawn.Spawned) return;
            float radius = GetRadiusForPawn();
            var victims = targets.Where(t => t.Thing is Pawn p && !p.Dead && p.Spawned && p.Map == pawn.Map
                    && p != pawn && DeepTargetUtility.IsEdible(p))
                .SelectMany(t => radius == 0 ? new[] { (Pawn)t.Thing }
                    : pawn.Map.mapPawns.AllPawnsSpawned.Where(p => p != pawn && !p.Dead
                        && p.Position.InHorDistOf(t.Cell, radius) && DeepTargetUtility.IsEdible(p)))
                .Distinct().ToArray();
            if (victims.Length == 0) return;
            // Freeze the complete bill before any offerings can increase
            // devotion. A powerful victim endangers the entire cast.
            double devotion = Devotion;
            int burnRank = Cultivation.Rank(pawn, BudUpgradeKind.ConsumeBurn);
            double heat = victims.Sum(v => HeatFor(v, devotion, burnRank, radius));
            base.Cast(targets);
            var ext = Ext;
            bool overflow = pawn.psychicEntropy == null || heat > float.MaxValue || double.IsNaN(heat)
                || pawn.psychicEntropy.WouldOverflowEntropy((float)heat);
            if (!overflow) pawn.psychicEntropy.TryAddEntropy((float)heat, pawn, scale: false);
            foreach (var victim in victims)
                if (!victim.Dead && victim.Spawned)
                    Devour(victim, victim.BodySize * ext.liveOfferingFactor);
            if (overflow && !pawn.Dead && pawn.Spawned)
            {
                Messages.Message("Deepcaller_CasterConsumed".Translate(pawn.Named("CASTER")),
                    new LookTargets(pawn.Position, pawn.Map), MessageTypeDefOf.NegativeHealthEvent);
                pawn.Strip();
                Devour(pawn, pawn.BodySize * ext.liveOfferingFactor);
            }
        }

        /// Instant death, no corpse; the flesh becomes devotion. With the
        /// Deep Hoard passive learned, the indigestible bits — equipment,
        /// apparel, inventory — survive into the idol's belly for ransom.
        private void Devour(Pawn victim, float bodySizeEquivalent)
        {
            if (HasDeepHoard)
                CompIdolDevotion.BestIdol(victim.MapHeld ?? pawn.Map, pawn.Faction)?.StashGear(victim);
            DeepDevourUtility.Devour(victim, pawn.Faction, bodySizeEquivalent);
        }

        private bool HasDeepHoard =>
            pawn.GetComp<VEF.Abilities.CompAbilities>()?.LearnedAbilities
                ?.Any(a => a.def == Deepcaller_DefOf.Deepcaller_DeepHoard) ?? false;

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
