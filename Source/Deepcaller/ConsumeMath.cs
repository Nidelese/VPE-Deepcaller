using System;

namespace Deepcaller
{
    public static class ConsumeMath
    {
        public static double RawHeat(double health, double marketValue, double combatPower, double damageFactor, int level,
            double heatPerHitPoint = 0.4) =>
            (20 + Math.Max(1, health) * heatPerHitPoint + Math.Max(0, marketValue) * 0.04
                + Math.Max(0, combatPower) * 0.5) * Math.Max(1, damageFactor)
                * (1 + Math.Pow(Math.Max(0, level - 1.0) / 50, 2));

        public static double Heat(double rawHeat, double devotion, int burnRank, double radius)
        {
            if (double.IsNaN(rawHeat) || double.IsNaN(devotion) || double.IsInfinity(rawHeat)) return double.PositiveInfinity;
            devotion = Math.Max(0, devotion);
            double logDivisor = devotion <= 800 ? Math.Log(1 + Math.Pow(devotion / 800, 2))
                : 2 * (Math.Log(devotion) - Math.Log(800)) + Math.Log(1 + Math.Pow(800 / devotion, 2));
            double logHeat = Math.Log(Math.Max(0.0001, rawHeat)) + Math.Log(1 + radius * radius / 25)
                - logDivisor - Math.Log(CultivationMath.Factor(burnRank, 0.2f));
            return Math.Max(0.0001, Math.Exp(logHeat));
        }
    }
}
