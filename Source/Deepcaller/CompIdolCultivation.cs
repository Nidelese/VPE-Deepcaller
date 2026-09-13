using System;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Deepcaller
{
    public partial class CompIdolDevotion
    {
        public CultivationProgress Progress => Cultivation.For(parent.Faction);
        public int IdolTier => CultivationMath.IdolTier(devotion);
        public string IdolTitle => ("Deepcaller_IdolTier" + IdolTier).Translate().ToString()
            + (devotion >= 4000 ? " · " + "Deepcaller_Depth".Translate(
                Math.Floor(Math.Log(devotion / 2000, 2)).ToString("0")).ToString() : "");
        public int DigestionInterval => Math.Max(1, (int)Math.Ceiling(
            Props.consumeIntervalTicks / (250.0 * (1 + IdolTier)))) * 250;
        public int DigestionBatch => 1 + IdolTier / 2 + (int)Math.Min(28,
            Math.Max(0, Math.Floor(Math.Log(Math.Max(2000, devotion) / 2000, 2))));

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            ImportLegacyCultivation();
        }

        public void ImportLegacyCultivation()
        {
            var progress = Progress;
            if (progress == null) return;
            progress.Import(BudUpgradeKind.Damage, budDamageLevel);
            progress.Import(BudUpgradeKind.FireRate, budFireRateLevel);
            progress.Import(BudUpgradeKind.BoltSpeed, budBoltSpeedLevel);
        }

        public double LogUpgradePrice(BudUpgradeKind kind, double? atDevotion = null) =>
            CultivationMath.LogPrice(BudUpgradeBaseGold(kind), Props.budUpgradeCostMultiplier,
                Progress?.Rank(kind) ?? 0, atDevotion ?? devotion, Props.budUpgradeDevotionPerDiscount);

        public bool TryUpgradePrice(BudUpgradeKind kind, out int cost)
        {
            cost = 0;
            return (Progress?.Rank(kind) ?? 0) < Cultivation.RankLimit(kind)
                && CultivationMath.TryPrice(LogUpgradePrice(kind), out cost);
        }

        public bool RangeCoversMap(BudUpgradeKind kind)
        {
            if (parent.MapHeld == null) return false;
            if (kind == BudUpgradeKind.ConsumeArea) return (Progress?.Rank(kind) ?? 0) >= parent.MapHeld.Size.LengthHorizontal;
            if (kind != BudUpgradeKind.RiptideArea && kind != BudUpgradeKind.RiptideReach) return false;
            var ability = DefDatabase<VEF.Abilities.AbilityDef>.GetNamed("Deepcaller_Riptide");
            var ext = ability.GetModExtension<AbilityExtension_Riptide>();
            double bonus = Math.Min(Math.Floor(TotalDevotion / Math.Max(1, ext.devotionPerBonusCell)), ext.maxBonusCells);
            bool area = kind == BudUpgradeKind.RiptideArea;
            double baseline = ability.range + bonus;
            if (area)
            {
                var curve = ext.radiusByDevotion;
                var last = curve.Points.Last();
                baseline = RiptideMath.ExtendedRadius(TotalDevotion, curve.Evaluate(Devotion), last.x, last.y);
            }
            return baseline + RiptideMath.ReachExtension(TotalDevotion, Progress?.Rank(kind) ?? 0, area)
                >= parent.MapHeld.Size.LengthHorizontal;
        }

        public double RequiredDevotion(BudUpgradeKind kind, long availableGold) =>
            Math.Max(Cultivation.UnlockDevotion(kind),
                CultivationMath.DevotionNeeded(BudUpgradeBaseGold(kind), Props.budUpgradeCostMultiplier,
                    Progress?.Rank(kind) ?? 0, Math.Min(int.MaxValue, availableGold), Props.budUpgradeDevotionPerDiscount));

        public bool PurchaseUpgrade(BudUpgradeKind kind)
        {
            if (!parent.Spawned || parent.Faction != RimWorld.Faction.OfPlayer || Progress == null
                || !Enum.IsDefined(typeof(BudUpgradeKind), kind) || Progress.Rank(kind) >= Cultivation.RankLimit(kind)
                || TotalDevotion < Cultivation.UnlockDevotion(kind)
                || UpgradeSaturated(kind)
                || !TryUpgradePrice(kind, out int cost) || GoldInShadow() < cost) return false;
            ConsumeGold(cost);
            Progress.Purchase(kind);
            // Movement is cached by the game, so invalidate ordinary tentacles.
            if (kind == BudUpgradeKind.MoveSpeed)
                foreach (var map in Find.Maps)
                    foreach (var pawn in map.mapPawns.SpawnedPawnsInFaction(parent.Faction))
                        if (pawn.def.defName == "Deepcaller_Tentacle") StatDefOf.MoveSpeed.Worker.ClearCacheForThing(pawn);
            if (kind == BudUpgradeKind.NonOrganicTargets)
                Celebrate("Deepcaller_NonOrganicUnlocked".Translate());
            else if (Cultivation.IsRiptideFavor(kind))
                Celebrate(("Deepcaller_" + kind + "Unlocked").Translate());
            else if (Progress.Rank(kind) % 5 == 0)
            {
                Celebrate("Deepcaller_CultivationMilestone".Translate(Cultivation.Label(kind), Progress.Rank(kind)));
            }
            else MoteMaker.ThrowText(parent.DrawPos, parent.Map,
                "Deepcaller_BudUpgradeBought".Translate(Cultivation.Label(kind)), 3.65f);
            return true;
        }

        private void Celebrate(string message)
        {
            if (!parent.Spawned) return;
            Thing_AbilityVisual.SpawnRiptide(parent.Map, parent.Position, 2 + IdolTier * 0.2f, 90);
            DefDatabase<SoundDef>.GetNamedSilentFail("VPE_Enthrall_Cast")?.PlayOneShot(
                new TargetInfo(parent.Position, parent.Map));
            Messages.Message(message, parent, MessageTypeDefOf.PositiveEvent, historical: true);
        }

        private void AddOffering(double gained)
        {
            if (gained <= 0 || double.IsNaN(gained) || double.IsInfinity(gained)) return;
            double before = devotion;
            double milestone = CultivationMath.NextIdolMilestone(before);
            devotion = Math.Min(double.MaxValue, devotion + gained);
            if (devotion >= milestone) Celebrate("Deepcaller_IdolAwakens".Translate(IdolTitle));
            var progress = Progress;
            if (progress == null || progress.pinned < 0) return;
            var kind = (BudUpgradeKind)progress.pinned;
            double oldLog = LogUpgradePrice(kind, before), newLog = LogUpgradePrice(kind);
            // Keep the unrounded reduction visible when both integer prices
            // still round to the same gold amount. No per-corpse message spam.
            string Price(double log) => CultivationMath.TryPrice(log, out int price)
                ? price.ToString("N0") : "Deepcaller_PriceTooHigh".Translate().ToString();
            progress.lastOfferingReceipt = "Deepcaller_OfferingReceipt".Translate(
                Cultivation.Number(gained), Cultivation.Label(kind), Price(oldLog), Price(newLog),
                ((1 - Math.Exp(newLog - oldLog)) * 100).ToString("0.######"));
        }

        public override void PostDraw()
        {
            base.PostDraw();
            if (parent.Spawned && IdolTier > 0)
                DeepcallerVisualRenderer.DrawIdolAura(parent.DrawPos, IdolTier,
                    Find.TickManager.TicksGame + parent.thingIDNumber);
        }
    }
}
