using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Deepcaller
{
    // Append new values: indices are persisted in saves (0..6 are the 1.1 lines).
    public enum BudUpgradeKind { Damage, FireRate, BoltSpeed, Feeding, Regeneration, MaturePower, MoveSpeed, NonOrganicTargets, RiptideRestraint, RiptideDiscernment, RiptideAcceleration, RiptidePossessions, RiptideArea, RiptideReach, HoardBargaining, HoardSalvage, HoardRestoration, HoardCleansing, ConsumeBurn, ConsumeArea }

    public class CultivationProgress : IExposable
    {
        private List<int> ranks = new List<int>();
        public int pinned = -1;
        public string lastOfferingReceipt;
        public int Revision { get; private set; }
        public int Rank(BudUpgradeKind kind) => ranks != null && (int)kind >= 0 && (int)kind < ranks.Count
            ? Math.Max(0, ranks[(int)kind]) : 0;
        public void Import(BudUpgradeKind kind, int rank)
        {
            ranks ??= new List<int>();
            while (ranks.Count <= (int)kind) ranks.Add(0);
            if (rank > ranks[(int)kind]) { ranks[(int)kind] = rank; Revision++; }
        }
        public void Purchase(BudUpgradeKind kind)
        {
            Import(kind, checked(Rank(kind) + 1));
            if (pinned == (int)kind && Rank(kind) >= Cultivation.RankLimit(kind)) pinned = -1;
            lastOfferingReceipt = null;
        }
        public void ExposeData()
        {
            Scribe_Collections.Look(ref ranks, "ranks", LookMode.Value);
            Scribe_Values.Look(ref pinned, "pinned", -1);
            Scribe_Values.Look(ref lastOfferingReceipt, "lastOfferingReceipt");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                ranks ??= new List<int>();
                if (pinned < -1 || pinned >= Enum.GetValues(typeof(BudUpgradeKind)).Length) pinned = -1;
            }
        }
    }

    public static class Cultivation
    {
        public static int RankLimit(BudUpgradeKind kind) =>
            kind == BudUpgradeKind.NonOrganicTargets || kind == BudUpgradeKind.RiptideRestraint
                || kind == BudUpgradeKind.RiptideDiscernment || kind == BudUpgradeKind.RiptidePossessions
                || kind == BudUpgradeKind.HoardSalvage || kind == BudUpgradeKind.HoardCleansing ? 1 : int.MaxValue;
        public static bool IsHoard(BudUpgradeKind kind) => kind >= BudUpgradeKind.HoardBargaining && kind <= BudUpgradeKind.HoardCleansing;
        public static bool IsRiptideFavor(BudUpgradeKind kind) => kind == BudUpgradeKind.RiptideRestraint
            || kind == BudUpgradeKind.RiptideDiscernment || kind == BudUpgradeKind.RiptidePossessions;
        public static double UnlockDevotion(BudUpgradeKind kind) =>
            kind == BudUpgradeKind.RiptideArea || kind == BudUpgradeKind.RiptideReach ? 5000
                : kind == BudUpgradeKind.ConsumeArea ? 2000 : 0;
        public static CultivationProgress For(Faction faction) => faction == Faction.OfPlayer
            ? DeepcallerGameComponent.Instance?.cultivation : null;
        public static int Rank(Thing thing, BudUpgradeKind kind) => For(thing?.Faction)?.Rank(kind) ?? 0;
        public static string Label(BudUpgradeKind kind) => ("Deepcaller_Cultivation" + kind).Translate();
        public static float Factor(Thing thing, BudUpgradeKind kind, float step = 0.12f) =>
            CultivationMath.Factor(Rank(thing, kind), step);
        public static string Number(double value) => double.IsInfinity(value) ? "Deepcaller_BeyondMeasure".Translate().ToString()
            : value >= 1000000 ? value.ToString("0.###E+0") : value.ToString("0.##");
    }
}
