using System;

namespace Deepcaller
{
    public struct CurrentVector
    {
        public double x, z;
        public CurrentVector(double x, double z) { this.x = x; this.z = z; }
        public double Length => Math.Sqrt(x * x + z * z);
        public CurrentVector Unit => Length > 1e-9 ? this / Length : default;
        public static CurrentVector operator +(CurrentVector a, CurrentVector b) => new CurrentVector(a.x + b.x, a.z + b.z);
        public static CurrentVector operator -(CurrentVector a, CurrentVector b) => new CurrentVector(a.x - b.x, a.z - b.z);
        public static CurrentVector operator *(CurrentVector a, double b) => new CurrentVector(a.x * b, a.z * b);
        public static CurrentVector operator /(CurrentVector a, double b) => a * (1 / b);
        public double Dot(CurrentVector b) => x * b.x + z * b.z;
    }

    // Positions and velocities used here are also used for actual movement.
    // Swept grid contacts handle the engine's discrete occupied cells.
    public static class RiptideKinetics
    {
        public const double MaximumSpeed = 8; // cells/tick, up to 32 swept steps
        public const double RestSpeed = 0.015;
        public const double Restitution = 0.15;

        public static CurrentVector Bounded(CurrentVector velocity)
        {
            if (double.IsNaN(velocity.x) || double.IsNaN(velocity.z)
                || double.IsInfinity(velocity.x) || double.IsInfinity(velocity.z)) return default;
            return velocity.Length > MaximumSpeed ? velocity.Unit * MaximumSpeed : velocity;
        }

        public static double Acceleration(double devotion, double mass, double force, int baseTicks) =>
            Math.Min(MaximumSpeed, RiptideMath.Acceleration(devotion, mass, force) / Math.Pow(Math.Max(1, baseTicks), 2));

        public static double AirDrag(double mass) => 0.02 / Math.Max(0.25, Math.Pow(RiptideMath.Mass(mass) / 70, 1.0 / 3));

        public static bool Crossed(CurrentVector position, CurrentVector center, CurrentVector startingDirection) =>
            (position - center).Dot(startingDirection) >= -1e-9;

        public static void Coast(ref CurrentVector position, ref CurrentVector velocity, double mass, double time)
        {
            double drag = AirDrag(mass);
            double retained = Math.Exp(-drag * Math.Max(0, time));
            position += velocity * ((1 - retained) / drag);
            velocity *= retained;
        }

        public static void Advance(ref CurrentVector position, ref CurrentVector velocity, ref bool crossed,
            CurrentVector center, CurrentVector startingDirection, double acceleration, double mass, double time, bool driven)
        {
            velocity = Bounded(velocity);
            if (!driven || crossed) { Coast(ref position, ref velocity, mass, time); return; }
            var a = (center - position).Unit * acceleration;
            // Split the integration at the center plane. Acceleration ends
            // there; velocity is retained, with air drag only on the remainder.
            var endPosition = position;
            var endVelocity = velocity;
            Powered(ref endPosition, ref endVelocity, a, time);
            if (Crossed(endPosition, center, startingDirection))
            {
                double low = 0, high = time;
                for (int i = 0; i < 32; i++)
                {
                    double mid = (low + high) * 0.5;
                    var at = position;
                    var speed = velocity;
                    Powered(ref at, ref speed, a, mid);
                    if (Crossed(at, center, startingDirection)) high = mid; else low = mid;
                }
                Powered(ref position, ref velocity, a, high);
                crossed = true;
                Coast(ref position, ref velocity, mass, time - high);
            }
            else { position = endPosition; velocity = endVelocity; }
        }

        private static void Powered(ref CurrentVector position, ref CurrentVector velocity, CurrentVector a, double time)
        {
            double a2 = a.Dot(a);
            if (a2 < 1e-15) { position += velocity * time; return; }
            double dot = velocity.Dot(a);
            double limitTime = (-dot + Math.Sqrt(Math.Max(0, dot * dot
                + a2 * (MaximumSpeed * MaximumSpeed - velocity.Dot(velocity))))) / a2;
            double acceleratingTime = Math.Min(time, Math.Max(0, limitTime));
            var next = Bounded(velocity + a * acceleratingTime);
            position += (velocity + next) * (acceleratingTime * 0.5) + next * (time - acceleratingTime);
            velocity = next;
        }

        public static float Damage(float pressure, double contactMass, double closingSpeed) =>
            (float)Math.Min(RiptideMath.MaximumDamage, Math.Max(0, pressure)
                * RiptideMath.Mass(contactMass) / 70 * Math.Pow(Math.Max(0, closingSpeed), 2) * 0.6);

        public static void Collide(ref CurrentVector first, ref CurrentVector second, CurrentVector normal,
            double firstMass, double secondMass, bool anchored, bool firstDriven, bool secondDriven)
        {
            normal = normal.Unit;
            double closing = (first - second).Dot(normal);
            if (closing <= 0) return;
            firstMass = RiptideMath.Mass(firstMass);
            secondMass = RiptideMath.Mass(secondMass);
            double impulse = (1 + Restitution) * closing
                / (1 / firstMass + (anchored ? 0 : 1 / secondMass));
            // Before center, the god replenishes impact losses. Once its
            // acceleration ends, ordinary mass-weighted momentum transfer wins.
            if (!firstDriven) first = Bounded(first - normal * (impulse / firstMass));
            if (!anchored && !secondDriven) second = Bounded(second + normal * (impulse / secondMass));
        }
    }
}
