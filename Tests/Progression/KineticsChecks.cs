using Deepcaller;

static class KineticsChecks
{
    public static void Run(Action<bool, string> check)
    {
        var center = new CurrentVector(0, 0);
        var direction = new CurrentVector(-1, 0);
        var p = new CurrentVector(10, 0);
        var v = new CurrentVector(-1, 0);
        bool crossed = false;
        RiptideKinetics.Advance(ref p, ref v, ref crossed, center, direction, 1, 70, 1, true);
        check(Math.Abs(p.x - 8.5) < 1e-8 && v.x == -2 && !crossed, "Before center: real displacement integrates acceleration without drag");
        p = new CurrentVector(.1, 0); v = direction; crossed = false;
        RiptideKinetics.Advance(ref p, ref v, ref crossed, center, direction, .5, 70, 1, true);
        check(crossed && p.x < 0 && v.x < -1, "Crossing the center retains incoming velocity");
        double speed = v.Length;
        RiptideKinetics.Advance(ref p, ref v, ref crossed, center, direction, 8, 70, 1, true);
        check(v.Length < speed && v.Length > speed * .97 && p.x < -1, "After center: only gentle air drag acts during free flight");

        foreach (double acceleration in new[] { .01, .3, 2.0, 8.0 })
        foreach (double initialSpeed in new[] { 0, 1.0, 7.9 })
        {
            var whole = new CurrentVector(2, 0); var wholeV = direction * initialSpeed; bool wholeCross = false;
            var parts = whole; var partsV = wholeV; bool partsCross = false;
            RiptideKinetics.Advance(ref whole, ref wholeV, ref wholeCross, center, direction, acceleration, 70, 1, true);
            for (int step = 0; step < 128; step++)
                RiptideKinetics.Advance(ref parts, ref partsV, ref partsCross, center, direction, acceleration, 70, 1.0 / 128, true);
            check((whole - parts).Length < 1e-6 && (wholeV - partsV).Length < 1e-6,
                "Splitting a tick preserves distance and speed, including center crossing and speed limits");
        }

        foreach (bool driven in new[] { false, true })
        {
            var truck = new CurrentVector(3, 0); var bar = new CurrentVector(0, 0);
            RiptideKinetics.Collide(ref truck, ref bar, new CurrentVector(1, 0), 450, .008, false, driven, false);
            check(truck.x > 2.99 && bar.x > 3, "A tiny gold bar cannot stop a truck; it receives momentum");
            var human = new CurrentVector(1, 0); var wall = new CurrentVector(0, 0);
            RiptideKinetics.Collide(ref human, ref wall, new CurrentVector(1, 0), 70, 0, true, driven, false);
            check(driven ? human.x == 1 : Math.Abs(human.x + .15) < 1e-9,
                "Wall impact preserves god-driven velocity before center and removes most speed afterward");
        }
        foreach (double firstMass in new[] { .008, 25, 70, 450.0 })
        foreach (double secondMass in new[] { .008, 25, 70, 450.0 })
        {
            var first = new CurrentVector(1, .1); var second = new CurrentVector(-.2, .1);
            double momentum = firstMass * first.x + secondMass * second.x;
            double energy = firstMass * first.Dot(first) + secondMass * second.Dot(second);
            RiptideKinetics.Collide(ref first, ref second, new CurrentVector(1, 0), firstMass, secondMass, false, false, false);
            check(Math.Abs(firstMass * first.x + secondMass * second.x - momentum) < 1e-8,
                "Unpowered collision conserves momentum across very different masses");
            check(firstMass * first.Dot(first) + secondMass * second.Dot(second) <= energy + 1e-8,
                "Unpowered splats dissipate kinetic energy");
        }
        check(RiptideKinetics.Damage(80, 450, .01) < 1, "Slow heavy contact remains mild before armor");
        check(RiptideKinetics.Damage(8000, 70, 0) == 0, "No relative motion means no collision injury");
        double a = RiptideKinetics.Acceleration(8000, 70, 1, 5);
        check(RiptideKinetics.Damage(8000, 70, Math.Sqrt(2 * a * 3)) >= 8000,
            "8,000 devotion and three clear edge-starting cells can break plasteel");
        foreach (double d in new[] { 10, 8000.0 })
        {
            double human = 70 * RiptideKinetics.Acceleration(d, 70, 1, 5);
            double truck = 450 * RiptideKinetics.Acceleration(d, 450, 1, 5);
            check(d == 10 ? truck < human : truck > human,
                "Heavy mass gives early resistance and stronger late impacts for equal run-up");
        }
    }
}
