using System;
using System.Collections.Generic;

namespace Deepcaller
{
    // No engine dependencies: the economy and firing clock are tested directly.
    public static class CultivationMath
    {
        public struct BudProfile
        {
            public double desiredInterval, interval, speedFactor, damageFactor, rateOverflow, speedOverflow, echoFactor;
        }
        public static BudProfile BudStats(double baseInterval, double baseSpeedFactor, int damage, int rate, int speed)
        {
            double desiredInterval = Math.Max(1e-9, baseInterval / Factor(rate));
            double interval = Math.Max(1.0 / 3, desiredInterval);
            double desiredSpeed = Math.Max(0.1, baseSpeedFactor * Factor(speed));
            double actualSpeed = Math.Min(10, desiredSpeed);
            return new BudProfile
            {
                desiredInterval = desiredInterval, interval = interval, speedFactor = actualSpeed,
                rateOverflow = interval / desiredInterval, speedOverflow = desiredSpeed / actualSpeed,
                damageFactor = Factor(damage) * interval / desiredInterval * desiredSpeed / actualSpeed,
                echoFactor = rate >= 5 ? 0.35 + 0.05 * (rate / 5) : 0,
            };
        }

        public static double LogPrice(int baseGold, double compound, int rank, double devotion, double scale)
        {
            if (baseGold < 1 || compound <= 1 || rank < 0 || scale <= 0
                || double.IsNaN(devotion) || devotion < 0 || double.IsInfinity(devotion))
                return double.PositiveInfinity;
            // Dividing first may overflow at extreme magnitudes; subtraction of
            // logarithms keeps the discount and compounded price representable.
            double logDiscount = devotion > scale
                ? Math.Log(devotion) - Math.Log(scale) + Math.Log(1 + scale / devotion)
                : Math.Log(1 + devotion / scale);
            return Math.Log(baseGold) + rank * Math.Log(compound) - logDiscount;
        }

        public static bool TryPrice(double logPrice, out int gold)
        {
            gold = 0;
            if (double.IsNaN(logPrice) || logPrice > Math.Log(int.MaxValue)) return false;
            var amount = Math.Ceiling(Math.Min(int.MaxValue, Math.Exp(Math.Max(0, logPrice))) - 1e-9);
            if (amount > int.MaxValue) return false;
            gold = (int)Math.Max(1, amount);
            return true;
        }

        public static double DevotionNeeded(int baseGold, double compound, int rank, long gold, double scale)
        {
            if (gold <= 0) return double.PositiveInfinity;
            var logRatio = Math.Log(baseGold) + rank * Math.Log(compound) - Math.Log(gold);
            if (logRatio <= 0) return 0;
            var logNeeded = Math.Log(scale) + logRatio;
            return logNeeded >= Math.Log(double.MaxValue) ? double.PositiveInfinity
                : Math.Max(0, Math.Exp(logNeeded) - scale);
        }

        public static float Factor(int rank, float step = 0.12f) =>
            1f + Math.Max(0, rank) * step + (Math.Max(0, rank) / 5) * 0.1f;

        public static float ReconcileGrowth(float fed, int earnedStage, IList<float> thresholds)
        {
            int stage = Math.Max(0, Math.Min(earnedStage, thresholds.Count));
            return Math.Max(fed, stage > 0 ? thresholds[stage - 1] : 0);
        }

        public static float CreditDamage(float actualDamage, float maximumHealth, ref float credited)
        {
            float earned = Math.Min(Math.Max(0, actualDamage), Math.Max(0, maximumHealth - credited));
            credited += earned;
            return earned;
        }

        public static int LifetimeGain(int requested, int elapsed, int alreadyGranted, int originalLifespan) =>
            Math.Max(0, Math.Min(elapsed, Math.Min(requested, originalLifespan - alreadyGranted)));

        // Carry fractional ticks between shots. Each purchase changes the
        // average cadence, even if its interval is not an integer tick count.
        public static bool AdvanceClock(ref double remaining, double interval)
        {
            remaining -= 1;
            if (remaining > 0) return false;
            remaining = Math.Max(remaining, -1) + Math.Max(1, interval);
            return true;
        }

        public static int IdolTier(double devotion)
        {
            double[] milestones = { 10, 30, 75, 175, 400, 900, 2000 };
            int tier = 0;
            foreach (var milestone in milestones) if (devotion >= milestone) tier++;
            return tier;
        }

        public static double NextIdolMilestone(double devotion)
        {
            double[] milestones = { 10, 30, 75, 175, 400, 900, 2000 };
            foreach (var milestone in milestones) if (devotion < milestone) return milestone;
            return Math.Pow(2, Math.Floor(Math.Log(devotion / 2000, 2)) + 1) * 2000;
        }
    }
}
