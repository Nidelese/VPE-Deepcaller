using System;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Deepcaller
{
    public class Dialog_Cultivation : Window
    {
        private readonly CompIdolDevotion idol;
        private Vector2 scroll;
        private readonly bool hoardOnly;
        public override Vector2 InitialSize => new Vector2(870, 760);
        public Dialog_Cultivation(CompIdolDevotion idol, bool hoardOnly = false)
        {
            this.idol = idol;
            this.hoardOnly = hoardOnly;
            doCloseX = true;
            doCloseButton = true;
            absorbInputAroundWindow = true;
            forcePause = true;
        }

        public override void DoWindowContents(Rect rect)
        {
            if (!idol.parent.Spawned || idol.Progress == null) { Close(); return; }
            var progress = idol.Progress;
            long gold = idol.GoldInShadow();
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, 0, rect.width - 30, 35), (hoardOnly ? "Deepcaller_HoardUpgrades" : "Deepcaller_CultivationTitle").Translate());
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0, 40, rect.width, 50), "Deepcaller_CultivationHeader".Translate(
                gold.ToString("N0"), Cultivation.Number(idol.TotalDevotion), Cultivation.Number(idol.BudGoldDiscount)));
            var viewport = new Rect(0, 96, rect.width, rect.height - 235);
            var kinds = Enum.GetValues(typeof(BudUpgradeKind)).Cast<BudUpgradeKind>()
                .Where(k => Cultivation.IsHoard(k) == hoardOnly).ToArray();
            var content = new Rect(0, 0, viewport.width - 18, kinds.Length * 130);
            Widgets.BeginScrollView(viewport, ref scroll, content);
            for (int index = 0; index < kinds.Length; index++)
            {
                var kind = kinds[index];
                float y = index * 130;
                int rank = progress.Rank(kind);
                bool unlocked = rank >= Cultivation.RankLimit(kind);
                Widgets.DrawBoxSolid(new Rect(0, y, content.width, 122), new Color(0.13f, 0.19f, 0.2f));
                Widgets.Label(new Rect(12, y + 6, content.width - 195, 26),
                    "Deepcaller_CultivationRank".Translate(Cultivation.Label(kind), rank));
                Widgets.Label(new Rect(12, y + 33, content.width - 195, 49), Cultivation.IsRiptideFavor(kind)
                    ? (rank == 0 ? "Deepcaller_Preview" + kind : "Deepcaller_" + kind
                        + (idol.Hungry ? "Hungry" : "Active")).Translate().ToString()
                    : Preview(kind, rank, idol.Devotion, progress));
                Widgets.Label(new Rect(12, y + 85, content.width - 195, 33), kind == BudUpgradeKind.NonOrganicTargets
                    ? "Deepcaller_NonOrganicUnlockDetails".Translate()
                    : Cultivation.IsRiptideFavor(kind)
                        ? ("Deepcaller_" + kind + "Details").Translate(idol.Props.hungerGraceDays.ToString("0.#"))
                    : Cultivation.IsHoard(kind)
                        ? "Deepcaller_HoardUpgradeDetails".Translate((idol.PriceFactor * 100).ToString("0.###"))
                    : kind == BudUpgradeKind.ConsumeArea
                        ? "Deepcaller_ConsumeAreaDetails".Translate()
                    : Cultivation.UnlockDevotion(kind) > 0
                        ? "Deepcaller_RiptideReachDetails".Translate(Cultivation.Number(Cultivation.UnlockDevotion(kind)))
                    : "Deepcaller_NextCultivationMilestone".Translate(
                        ((long)rank / 5 + 1) * 5, ("Deepcaller_Milestone" + kind).Translate()));
                bool finite = idol.TryUpgradePrice(kind, out int price);
                bool devotionLocked = idol.TotalDevotion < Cultivation.UnlockDevotion(kind);
                bool coversMap = idol.RangeCoversMap(kind);
                bool saturated = idol.UpgradeSaturated(kind);
                bool canBuy = finite && price <= gold && !unlocked && !devotionLocked && !saturated;
                GUI.color = canBuy ? Color.white : Color.gray;
                if (Widgets.ButtonText(new Rect(content.width - 174, y + 8, 162, 42), unlocked
                    ? "Deepcaller_CultivationUnlocked".Translate() : coversMap
                    ? "Deepcaller_RiptideCoversMap".Translate() : saturated
                    ? "Deepcaller_UpgradeSaturated".Translate() : devotionLocked
                    ? "Deepcaller_CultivationNeedsDevotion".Translate(Cultivation.Number(Cultivation.UnlockDevotion(kind))) : finite
                    ? "Deepcaller_BuyGold".Translate(price.ToString("N0"))
                    : "Deepcaller_PriceTooHigh".Translate()) && canBuy)
                    idol.PurchaseUpgrade(kind);
                GUI.color = Color.white;
                if (!unlocked && Widgets.ButtonText(new Rect(content.width - 174, y + 63, 162, 36),
                    (progress.pinned == (int)kind ? "Deepcaller_Unpin" : "Deepcaller_Pin").Translate()))
                {
                    progress.pinned = progress.pinned == (int)kind ? -1 : (int)kind;
                    progress.lastOfferingReceipt = null;
                }
            }
            Widgets.EndScrollView();
            float bottom = viewport.yMax + 8;
            if (progress.pinned >= 0)
            {
                var kind = (BudUpgradeKind)progress.pinned;
                string target = gold == 0 ? "Deepcaller_PinnedNeedsGold".Translate().ToString()
                    : "Deepcaller_PinnedGoal".Translate(Cultivation.Label(kind), gold.ToString("N0"),
                        Cultivation.Number(Math.Max(0, idol.RequiredDevotion(kind, gold) - idol.TotalDevotion)));
                Widgets.Label(new Rect(0, bottom, rect.width, 45), target);
                Widgets.Label(new Rect(0, bottom + 45, rect.width, 48), progress.lastOfferingReceipt ?? "");
            }
            else Widgets.Label(new Rect(0, bottom, rect.width, 65), "Deepcaller_CultivationHelp".Translate());
        }

        private static string Preview(BudUpgradeKind kind, int rank, float devotion, CultivationProgress progress)
        {
            if (kind == BudUpgradeKind.ConsumeArea)
                return "Deepcaller_PreviewConsumeArea".Translate(rank, (long)rank + 1);
            if (kind == BudUpgradeKind.ConsumeBurn)
                return "Deepcaller_PreviewConsumeBurn".Translate(CultivationMath.Factor(rank, 0.2f).ToString("0.##"),
                    CultivationMath.Factor(rank == int.MaxValue ? rank : rank + 1, 0.2f).ToString("0.##"));
            if (Cultivation.IsHoard(kind))
            {
                int following = rank == int.MaxValue ? rank : rank + 1;
                return ("Deepcaller_Preview" + kind).Translate(
                    (100 / (1 + rank * 0.2)).ToString("0.##"),
                    (100 / (1 + following * 0.2)).ToString("0.##"),
                    (100 * HoardMath.Restoration(rank)).ToString("0.##"),
                    (100 * HoardMath.Restoration(following)).ToString("0.##"));
            }
            if (kind == BudUpgradeKind.RiptideArea || kind == BudUpgradeKind.RiptideReach)
            {
                bool area = kind == BudUpgradeKind.RiptideArea;
                return ("Deepcaller_Preview" + kind).Translate(
                    Cultivation.Number(RiptideMath.ReachExtension(devotion, rank, area)),
                    Cultivation.Number(RiptideMath.ReachExtension(devotion, rank == int.MaxValue ? rank : rank + 1, area)),
                    area ? 4 : 8);
            }
            if (kind == BudUpgradeKind.RiptideAcceleration)
                return "Deepcaller_PreviewRiptideAcceleration".Translate(
                    CultivationMath.Factor(rank, 0.1f).ToString("0.##"),
                    CultivationMath.Factor(rank == int.MaxValue ? rank : rank + 1, 0.1f).ToString("0.##"));
            if (kind == BudUpgradeKind.NonOrganicTargets)
                return (rank == 0 ? "Deepcaller_PreviewNonOrganicLocked" : "Deepcaller_PreviewNonOrganicUnlocked").Translate();
            int next = rank == int.MaxValue ? rank : rank + 1;
            var scaling = DefDatabase<ThingDef>.GetNamed("Deepcaller_Bud").GetModExtension<BudScalingExtension>();
            string Pair(double a, double b) => Cultivation.Number(a) + " → " + Cultivation.Number(b);
            CultivationMath.BudProfile Profile(bool after) => CultivationMath.BudStats(
                scaling.cooldownSecondsByDevotion.Evaluate(devotion), scaling.projectileSpeedFactorByDevotion.Evaluate(devotion),
                after && kind == BudUpgradeKind.Damage ? next : progress.Rank(BudUpgradeKind.Damage),
                after && kind == BudUpgradeKind.FireRate ? next : progress.Rank(BudUpgradeKind.FireRate),
                after && kind == BudUpgradeKind.BoltSpeed ? next : progress.Rank(BudUpgradeKind.BoltSpeed));
            var beforeStats = Profile(false);
            var afterStats = Profile(true);
            double baseDamage = scaling.damageByDevotion.Evaluate(devotion);
            double Damage(double factor) => Math.Min(int.MaxValue - 1.0, Math.Max(1, Math.Round(baseDamage * factor)));
            string payload = Pair(Damage(beforeStats.damageFactor), Damage(afterStats.damageFactor));
            float step = kind == BudUpgradeKind.MoveSpeed ? 0.1f : kind == BudUpgradeKind.Feeding
                || kind == BudUpgradeKind.Regeneration ? 0.15f : 0.12f;
            string factors = Pair(CultivationMath.Factor(rank, step), CultivationMath.Factor(next, step));
            switch (kind)
            {
                case BudUpgradeKind.Damage:
                    return "Deepcaller_PreviewDamage".Translate(payload);
                case BudUpgradeKind.FireRate:
                    return "Deepcaller_PreviewFireRate".Translate(Pair(beforeStats.interval, afterStats.interval),
                        payload, Pair(beforeStats.echoFactor * 100, afterStats.echoFactor * 100));
                case BudUpgradeKind.BoltSpeed:
                    return "Deepcaller_PreviewBoltSpeed".Translate(Pair(18 * beforeStats.speedFactor, 18 * afterStats.speedFactor), payload);
                case BudUpgradeKind.MoveSpeed:
                    return "Deepcaller_PreviewMoveSpeed".Translate(factors, Pair(Math.Min(60, 1.2f * CultivationMath.Factor(rank, step)),
                        Math.Min(60, 1.2f * CultivationMath.Factor(next, step))));
                default: return ("Deepcaller_Preview" + kind).Translate(factors);
            }
        }
    }
}
