using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace Deepcaller
{
    public class Dialog_Cultivation : Window
    {
        private readonly CompIdolDevotion idol;
        private Vector2 scroll;
        public override Vector2 InitialSize => new Vector2(870, 760);
        public Dialog_Cultivation(CompIdolDevotion idol)
        {
            this.idol = idol;
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
            Widgets.Label(new Rect(0, 0, rect.width - 30, 35), "Deepcaller_CultivationTitle".Translate());
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0, 40, rect.width, 50), "Deepcaller_CultivationHeader".Translate(
                gold.ToString("N0"), Cultivation.Number(idol.TotalDevotion), Cultivation.Number(idol.BudGoldDiscount)));
            var viewport = new Rect(0, 96, rect.width, rect.height - 235);
            var content = new Rect(0, 0, viewport.width - 18, 7 * 130);
            Widgets.BeginScrollView(viewport, ref scroll, content);
            foreach (BudUpgradeKind kind in Enum.GetValues(typeof(BudUpgradeKind)))
            {
                float y = (int)kind * 130;
                int rank = progress.Rank(kind);
                Widgets.DrawBoxSolid(new Rect(0, y, content.width, 122), new Color(0.13f, 0.19f, 0.2f));
                Widgets.Label(new Rect(12, y + 6, content.width - 195, 26),
                    "Deepcaller_CultivationRank".Translate(Cultivation.Label(kind), rank));
                Widgets.Label(new Rect(12, y + 33, content.width - 195, 49), Preview(kind, rank, idol.Devotion, progress));
                Widgets.Label(new Rect(12, y + 85, content.width - 195, 33),
                    "Deepcaller_NextCultivationMilestone".Translate(
                        ((long)rank / 5 + 1) * 5, ("Deepcaller_Milestone" + kind).Translate()));
                bool finite = idol.TryUpgradePrice(kind, out int price);
                bool canBuy = finite && price <= gold && rank < int.MaxValue;
                GUI.color = canBuy ? Color.white : Color.gray;
                if (Widgets.ButtonText(new Rect(content.width - 174, y + 8, 162, 42), finite
                    ? "Deepcaller_BuyGold".Translate(price.ToString("N0"))
                    : "Deepcaller_PriceTooHigh".Translate()) && canBuy)
                    idol.PurchaseUpgrade(kind);
                GUI.color = Color.white;
                if (Widgets.ButtonText(new Rect(content.width - 174, y + 63, 162, 36),
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
                    return "Deepcaller_PreviewMoveSpeed".Translate(factors, Pair(1.2f * CultivationMath.Factor(rank, step),
                        1.2f * CultivationMath.Factor(next, step)));
                default: return ("Deepcaller_Preview" + kind).Translate(factors);
            }
        }
    }
}
