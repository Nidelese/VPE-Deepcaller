using System;

namespace Deepcaller
{
    public static class RiptideMath
    {
        // Engine bounds, not a devotion-tier cap. Pressure continues scaling
        // through ordinary and extreme play without overflowing DamageInfo.
        public const float MaximumDamage = 1000000000;
        public const float MaximumRadius = 32;

        // Purchases raise the limiting reach, instead of merely getting to
        // the same limit sooner. At D=5,000 half of each extension is active.
        // Each rank adds 4 radius or 8 cast-distance cells to the asymptote.
        public static double ReachExtension(double devotion, int rank, bool area) =>
            Math.Max(0, rank) * (area ? 4.0 : 8.0) * (1 - 5000 / (5000 + Devotion(devotion)));

        public static float MapBoundedReach(double baseline, double extension, double mapExtent) =>
            (float)Math.Min(Math.Max(1, Devotion(mapExtent)), Devotion(baseline) + Devotion(extension));

        public static double Devotion(double value) => double.IsNaN(value) || value < 0 ? 0
            : double.IsPositiveInfinity(value) ? double.MaxValue : value;

        public static float Pressure(double devotion, float baseline, float perDevotion)
        {
            double value = Math.Max(0, baseline) + Devotion(devotion) * Math.Max(0, perDevotion);
            return double.IsNaN(value) ? 0 : (float)Math.Min(MaximumDamage, value);
        }

        // Work builds along the actual path traveled, not from proximity to
        // the cast center or time spent stuck. Three cells earn full base
        // pressure; seven earn double. Every further cell still adds force.
        public static float MomentumDamage(float basePressure, double cellsMoved)
        {
            if (float.IsNaN(basePressure) || basePressure <= 0) return 0;
            double factor = 0.25 + 0.25 * Devotion(cellsMoved);
            return (float)Math.Min(MaximumDamage, basePressure * factor);
        }

        public static double Mass(double kilograms) => Math.Max(0.000001, Math.Min(70000000, Devotion(kilograms)));

        // A weak current struggles to accelerate heavy bodies. As devotion
        // rises it entrains them, turning their inertia into destructive force.
        // F = m*a, normalized to a 70 kg body at the same devotion.
        private static double InertiaExponent(double devotion) =>
            1.15 - 0.65 * (1 - 800 / (800 + Devotion(devotion)));

        public static double StartForce(double distance, double radius) => radius <= 0 || double.IsNaN(radius)
            || double.IsInfinity(radius) || double.IsNaN(distance) || distance < 0 || distance > radius ? 0 : distance / radius;

        public static double Acceleration(double devotion, double massKg, double forceMultiplier = 1) =>
            (1 + Math.Log(1 + Devotion(devotion) / 80, 2))
                / Math.Pow(Math.Max(1, Mass(massKg) / 70), InertiaExponent(devotion)) * Devotion(forceMultiplier);

        public static int MassPullInterval(double devotion, double massKg, int baseTicks, double forceMultiplier = 1) =>
            (int)Math.Min(60, Math.Max(1, Math.Ceiling(Math.Max(1, baseTicks) / Acceleration(devotion, massKg, forceMultiplier))));

        public static float ImpactDamage(float basePressure, double cellsMoved, double devotion, double massKg, double forceMultiplier = 1)
        {
            if (float.IsNaN(basePressure) || basePressure <= 0 || Devotion(forceMultiplier) == 0) return 0;
            double mass = Mass(massKg) / 70;
            double force = mass * Acceleration(devotion, massKg) / Acceleration(devotion, 70);
            return (float)Math.Min(MaximumDamage, basePressure * (0.25 + 0.25 * Devotion(cellsMoved)) * force * Devotion(forceMultiplier));
        }

        public static double TravelSpeed(double devotion, double massKg, double distance, int baseTicks, double forceMultiplier = 1) =>
            Devotion(forceMultiplier) == 0 ? 0 : Math.Min(1, 0.25 + 0.25 * Devotion(distance))
                / MassPullInterval(devotion, massKg, baseTicks, forceMultiplier);

        public static double ContactMass(double movingMass, double otherMass, bool anchored) => anchored ? Mass(movingMass)
            : 2 * Mass(movingMass) * Mass(otherMass) / (Mass(movingMass) + Mass(otherMass));

        public static float CollisionDamage(float pressure, double distance, double devotion, double massKg, double relativeSpeed, double forceMultiplier = 1)
        {
            double speed = Math.Min(2, Devotion(relativeSpeed));
            return (float)Math.Min(MaximumDamage, ImpactDamage(pressure, distance, devotion, massKg, forceMultiplier) * speed * speed);
        }

        public static float ExtendedRadius(double devotion, float curveRadius, float lastDevotion, float lastRadius)
        {
            double value = curveRadius;
            if (lastDevotion > 0 && Devotion(devotion) > lastDevotion)
                value = lastRadius + 2 * (Math.Log(Devotion(devotion)) - Math.Log(lastDevotion)) / Math.Log(2);
            return double.IsNaN(value) ? 2 : (float)Math.Min(MaximumRadius, Math.Max(0.1, value));
        }

        // More pressure moves bodies faster; heavy creatures resist by mass,
        // not by organic/psychic flags. Actual movement is at most one cell/tick.
        public static int PullInterval(double devotion, float bodySize, int baseTicks)
        {
            double mass = float.IsNaN(bodySize) ? 1 : Math.Max(1, Math.Min(10000, bodySize));
            double force = 1 + Math.Log(1 + Devotion(devotion) / 80, 2);
            return (int)Math.Min(60, Math.Max(1, Math.Ceiling(Math.Max(1, baseTicks) * Math.Sqrt(mass) / force)));
        }
    }
}
