using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Deepcaller
{
    public class AbilityExtension_Symbiote : DefModExtension
    {
        // Max manipulation bonus = devotion / devotionPerBonusPoint at cast
        // time, capped. Fresh grafts start at startingBonus and adapt up
        // (rate lives on the hediff's SymbioteExtension).
        public float devotionPerBonusPoint = 100f;
        public float maxBonusPerGraft = 25f;
        public float startingBonus = -10f;

        // Chance the graft eats the caster instead, by number of grafts
        // already carried. Past the end of the list, the last entry holds.
        public List<float> failChances = new List<float> { 0f, 0.05f, 0.10f, 0.20f, 0.40f, 0.80f };

        public float liveOfferingFactor = 1.5f;
    }

    /// Graft a tentacle of the deep onto the caster. Each graft raises the
    /// pawn's potential Manipulation (scaled by idol devotion at cast) but
    /// starts as a shock the pawn must grow into. Every additional graft
    /// tempts the deep: fail the roll and the tentacle eats its host.
    public class Ability_Symbiote : VEF.Abilities.Ability
    {
        private static readonly AbilityExtension_Symbiote DefaultExt = new AbilityExtension_Symbiote();

        private AbilityExtension_Symbiote Ext =>
            def.GetModExtension<AbilityExtension_Symbiote>() ?? DefaultExt;

        public override bool IsEnabledForPawn(out string reason)
        {
            if (!CompIdolDevotion.AnyIdol(pawn.MapHeld, pawn.Faction))
            {
                reason = "Deepcaller_ConsumeNoIdol".Translate();
                return false;
            }
            return base.IsEnabledForPawn(out reason);
        }

        private int GraftCount =>
            (pawn.health.hediffSet.GetFirstHediffOfDef(Deepcaller_DefOf.Deepcaller_Symbiote)
                as Hediff_Symbiote)?.GraftCount ?? 0;

        private float FailChanceFor(int grafts) =>
            Ext.failChances.Count == 0
                ? 0f
                : Ext.failChances[Mathf.Min(grafts, Ext.failChances.Count - 1)];

        /// The gamble should never be blind: the gizmo tooltip shows this
        /// caster's current rejection odds and what the graft is worth at
        /// today's devotion (RIP Diana, eaten on graft five).
        public override string GetDescriptionForPawn()
        {
            var ext = Ext;
            var grafts = GraftCount;
            var failChance = FailChanceFor(grafts);
            var devotion = CompIdolDevotion.HighestDevotion(pawn.MapHeld, pawn.Faction);
            var maxBonus = Mathf.Min(devotion / ext.devotionPerBonusPoint, ext.maxBonusPerGraft);

            var oddsColor = failChance <= 0f ? Color.cyan
                : failChance < 0.4f ? new Color(1f, 0.85f, 0.3f)
                : Color.red;
            return base.GetDescriptionForPawn()
                + "\n" + "Deepcaller_SymbioteOdds"
                    .Translate(grafts, failChance.ToStringPercent()).Colorize(oddsColor)
                + "\n" + "Deepcaller_SymbioteCap"
                    .Translate(maxBonus.ToString("0"), ext.startingBonus.ToString("0")).Colorize(Color.cyan);
        }

        public override void Cast(params GlobalTargetInfo[] targets)
        {
            base.Cast(targets);
            var ext = Ext;
            var existing = pawn.health.hediffSet.GetFirstHediffOfDef(Deepcaller_DefOf.Deepcaller_Symbiote)
                as Hediff_Symbiote;
            var failChance = FailChanceFor(existing?.GraftCount ?? 0);

            if (Rand.Chance(failChance))
            {
                Messages.Message("Deepcaller_SymbioteRejected".Translate(pawn.Named("CASTER")),
                    new LookTargets(pawn.Position, pawn.Map), MessageTypeDefOf.NegativeHealthEvent);
                pawn.Strip();
                DeepDevourUtility.Devour(pawn, pawn.Faction, pawn.BodySize * ext.liveOfferingFactor);
                return;
            }

            var devotion = CompIdolDevotion.HighestDevotion(pawn.Map, pawn.Faction);
            var maxBonus = Mathf.Min(devotion / ext.devotionPerBonusPoint, ext.maxBonusPerGraft);

            if (existing == null)
            {
                existing = (Hediff_Symbiote)HediffMaker.MakeHediff(Deepcaller_DefOf.Deepcaller_Symbiote, pawn);
                pawn.health.AddHediff(existing);
            }
            existing.AddGraft(maxBonus, ext.startingBonus);

            for (var i = 0; i < 5; i++)
                FleckMaker.ThrowDustPuffThick(pawn.DrawPos, pawn.Map, 2f, DeepPullUtility.DeepColor);
            MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "Deepcaller_SymbioteTaken".Translate(), 3.65f);
        }
    }
}
