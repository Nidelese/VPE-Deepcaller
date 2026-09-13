using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Deepcaller
{
    public class Hediff_DeepFeedingCredit : Hediff
    {
        public float maximumHealth;
        public float creditedDamage;
        public override bool Visible => false;
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref maximumHealth, "maximumHealth");
            Scribe_Values.Look(ref creditedDamage, "creditedDamage");
        }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.TakeDamage))]
    public static class Deepcaller_CombatFeeding
    {
        public struct CreditState
        {
            public CompTentacleGrowth growth;
            public Hediff_DeepFeedingCredit credit;
            public float body;
        }
        public static void Prefix(Thing __instance, DamageInfo dinfo, out CreditState __state)
        {
            __state = default;
            if (__instance is not Pawn victim || victim.Dead || dinfo.Instigator is not Pawn attacker
                || attacker.def.defName != "Deepcaller_Tentacle" || !attacker.Spawned || attacker.Dead
                || !victim.HostileTo(attacker) || victim.def.HasModExtension<TentacleRaceExtension>()
                || !dinfo.Def.harmsHealth) return;
            var growth = attacker.TryGetComp<CompTentacleGrowth>();
            if (growth == null) return;
            var def = DefDatabase<HediffDef>.GetNamed("Deepcaller_FeedingCredit");
            var credit = victim.health.hediffSet.GetFirstHediffOfDef(def) as Hediff_DeepFeedingCredit;
            if (credit == null)
            {
                credit = (Hediff_DeepFeedingCredit)victim.health.AddHediff(def);
                // One core-part health pool earns one body-size of combat
                // growth across all attackers; healing the prey never refills it.
                credit.maximumHealth = Mathf.Max(1, victim.RaceProps.body.corePart.def.GetMaxHealth(victim));
            }
            __state = new CreditState { growth = growth, credit = credit, body = victim.BodySize };
        }
        public static void Postfix(DamageWorker.DamageResult __result, CreditState __state)
        {
            if (__state.credit == null || __result == null || __state.credit.maximumHealth <= 0) return;
            var credit = __state.credit;
            float earned = CultivationMath.CreditDamage(__result.totalDamageDealt, credit.maximumHealth, ref credit.creditedDamage);
            __state.growth.QueueCombatFeed(__state.body * earned / credit.maximumHealth);
        }
    }

    public partial class CompTentacleGrowth
    {
        private float pendingCombatFeed;
        private int nextSpecialTick;
        private int slamTick = -1;
        private IntVec3 slamCell = IntVec3.Invalid;
        public string CombatDescription => ("Deepcaller_TentacleCombat" + Stage).Translate();
        private float HealingFactor => Cultivation.Factor(Pawn, BudUpgradeKind.Regeneration, 0.15f);
        public bool CanBenefitFromMeal => !FullyGrown
            || Pawn.health.hediffSet.hediffs.OfType<Hediff_Injury>().Any(i => i.Severity > 0)
            || Cultivation.Rank(Pawn, BudUpgradeKind.Regeneration) >= 5
                && Pawn.health.hediffSet.GetMissingPartsCommonAncestors().Any()
            || Pawn.TryGetComp<CompShield_Deep>()?.CanBenefitFromMeal == true
            || Pawn.TryGetComp<CompDeepReturn>()?.CanBenefitFromMeal == true;

        public void QueueCombatFeed(float points) => pendingCombatFeed += points;
        public void EatMeal(float bodySize)
        {
            Feed(bodySize * Props.corpseFeedFactor * Cultivation.Factor(Pawn, BudUpgradeKind.Feeding, 0.15f));
            Heal(bodySize * Props.healPerBodySizeConsumed * HealingFactor);
            Pawn.TryGetComp<CompShield_Deep>()?.FeedShield(bodySize * 0.15f * HealingFactor);
            Pawn.TryGetComp<CompDeepReturn>()?.FeedDuration((int)Mathf.Min(30000,
                bodySize * 2500 * Cultivation.Factor(Pawn, BudUpgradeKind.Feeding, 0.15f)));
            if (Cultivation.Rank(Pawn, BudUpgradeKind.Regeneration) >= 5)
            {
                var missing = Pawn.health.hediffSet.GetMissingPartsCommonAncestors().FirstOrDefault();
                if (missing != null) Pawn.health.RestorePart(missing.Part);
            }
            if (Pawn.Spawned) MoteMaker.ThrowText(Pawn.DrawPos, Pawn.Map,
                "Deepcaller_MealRestored".Translate(), 2f);
        }

        private void ExposeCombatData()
        {
            Scribe_Values.Look(ref pendingCombatFeed, "pendingCombatFeed");
            Scribe_Values.Look(ref nextSpecialTick, "nextSpecialTick");
            Scribe_Values.Look(ref slamTick, "slamTick", -1);
            Scribe_Values.Look(ref slamCell, "slamCell", IntVec3.Invalid);
        }

        public override void CompTick()
        {
            base.CompTick();
            // Exact race gate keeps Leviathan formation parts out of this system.
            if (Pawn.def.defName != "Deepcaller_Tentacle" || !Pawn.Spawned || Pawn.Dead) return;
            if (pendingCombatFeed > 0)
            {
                float earned = pendingCombatFeed;
                pendingCombatFeed = 0;
                Feed(earned * Props.killFeedFactor * Cultivation.Factor(Pawn, BudUpgradeKind.Feeding, 0.15f));
            }
            int now = Find.TickManager.TicksGame;
            if (Pawn.Downed || Pawn.stances.stunner.Stunned) { slamTick = -1; return; }
            if (slamTick >= 0)
            {
                if (now >= slamTick)
                {
                    slamTick = -1;
                    if (slamCell.InHorDistOf(Pawn.Position, 3)) StrikeArea(slamCell, 2.5f, 18);
                }
                return;
            }
            if (Stage == 0 || now < nextSpecialTick || !Pawn.IsHashIntervalTick(15)) return;
            var target = Pawn.mindState.enemyTarget as Pawn;
            float range = Stage == 1 ? 5 : Stage == 2 ? 1.9f : 3;
            bool InReach(Pawn enemy) => DeepTargetUtility.IsCombatTarget(enemy, Pawn)
                && enemy.Position.InHorDistOf(Pawn.Position, range)
                && GenSight.LineOfSight(Pawn.Position, enemy.Position, Pawn.Map);
            if (!InReach(target))
                target = Pawn.Map.mapPawns.AllPawnsSpawned.Where(InReach)
                    .OrderBy(enemy => enemy.Position.DistanceToSquared(Pawn.Position)).FirstOrDefault();
            if (target == null) return;
            nextSpecialTick = now + 480;
            if (Stage == 1)
            {
                DeepPullUtility.PullTowards(target, Pawn.Position, 2, 1.4f);
                DeepcallerGameComponent.QueueDamage(target,
                    3 * Cultivation.Factor(Pawn, BudUpgradeKind.MaturePower), Pawn);
                var gripDef = DefDatabase<HediffDef>.GetNamed("Deepcaller_Gripped");
                var grip = target.health.hediffSet.GetFirstHediffOfDef(gripDef) ?? target.health.AddHediff(gripDef);
                grip.Severity = Mathf.Max(grip.Severity, 0.12f);
                Thing_AbilityVisual.SpawnGraspRelease(Pawn.Map, target.Position);
            }
            else if (Stage == 2) StrikeArea(target.Position, 1.5f, 8);
            else
            {
                slamCell = target.Position;
                slamTick = now + 45;
                Thing_AbilityVisual.SpawnRiptide(Pawn.Map, slamCell, 2.5f, 45);
                MoteMaker.ThrowText(Pawn.DrawPos, Pawn.Map, "Deepcaller_SlamWarning".Translate(), 2f);
            }
        }

        private void StrikeArea(IntVec3 center, float radius, float damage)
        {
            var factor = Cultivation.Factor(Pawn, BudUpgradeKind.MaturePower);
            var nearby = GenRadial.RadialCellsAround(center, radius, true)
                .Where(c => c.InBounds(Pawn.Map)).SelectMany(c => c.GetThingList(Pawn.Map).OfType<Pawn>()).ToArray();
            foreach (var victim in nearby)
                if (DeepTargetUtility.IsCombatTarget(victim, Pawn) && victim.Position.InHorDistOf(center, radius)
                    && GenSight.LineOfSight(center, victim.Position, Pawn.Map))
                    DeepcallerGameComponent.QueueDamage(victim, Mathf.Min(1000000000, damage * factor), Pawn);
            Thing_AbilityVisual.SpawnShieldBreak(Pawn.Map, center, radius);
        }
    }

    public class StatPart_DeepcallerMovement : StatPart
    {
        private static bool Applies(StatRequest req) => req.Thing is Pawn pawn
            && pawn.def.defName == "Deepcaller_Tentacle";
        public override void TransformValue(StatRequest req, ref float val)
        {
            if (Applies(req)) val = Mathf.Min(60, val * Cultivation.Factor(req.Thing, BudUpgradeKind.MoveSpeed, 0.1f));
        }
        public override string ExplanationPart(StatRequest req) => Applies(req)
            ? "Deepcaller_MoveSpeedExplanation".Translate(Cultivation.Factor(req.Thing, BudUpgradeKind.MoveSpeed, 0.1f).ToString("0.##"))
            : null;
    }
}
