using System;

namespace Deepcaller
{
    public static class HoardMath
    {
        // Preserve the old bargains up to 2,000; every later offering improves
        // them too. Permanent ranks multiply this continuing devotion divisor.
        public static double Discount(double devotion, int bargainingRank) =>
            (1 + Math.Max(0, devotion - 2000) / 2000) * (1 + Math.Max(0, bargainingRank) * 0.2);
        public static double Restoration(int rank) => 1 - 1 / (1 + Math.Max(0, rank) * 0.25);
        public static int RestoredHitPoints(int current, int maximum, int rank) =>
            Math.Min(maximum, current + (int)Math.Ceiling(Math.Max(0, maximum - current) * Restoration(rank)));
    }
}
