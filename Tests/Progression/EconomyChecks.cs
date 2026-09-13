using Deepcaller;

static class EconomyChecks
{
    public static void Run(Action<bool, string> check)
    {
        double ordinary = ConsumeMath.RawHeat(350, 1750, 100, 1, 1);
        check(ordinary > 250, "Healthy ordinary prey exceeds a novice's heat budget before devotion");
        double at2000 = ConsumeMath.Heat(ordinary, 2000, 5, 1);
        double at5000 = ConsumeMath.Heat(ordinary, 5000, 5, 5);
        check(at2000 * 3 < 100 && at2000 * 6 > 100, "2,000 devotion: a small upgraded feast is viable but crowding is dangerous");
        check(at5000 * 12 < 100, "5,000 devotion: a five-tile ordinary feast can fit a 100-heat budget");
        double boss = ConsumeMath.RawHeat(350, 1750, 100, 1, 1600);
        check(ConsumeMath.Heat(boss, 5000, 5, 5) > 1000, "A level-1,600 Isekai boss overwhelms that same cast");
        check(ConsumeMath.Heat(boss, 500000, 5, 10) < 10, "Ungodly devotion eventually makes huge boss offerings viable");
        foreach (double devotion in new[] { 0, 80, 2000, 5000, 8000, 1e9, 1e100, double.MaxValue })
        {
            double heat = ConsumeMath.Heat(ordinary, devotion, 0, 0);
            check(double.IsFinite(heat) && heat > 0, "Consume remains finite and positive at extreme devotion");
            check(ConsumeMath.Heat(ordinary, devotion, 5, 0) <= heat, "Purchased burn relief never increases heat");
            check(ConsumeMath.Heat(ordinary, devotion, 0, 5) >= heat, "Widening the maw never lowers its heat bill");
            foreach (int price in new[] { 20, 25, 15, 30, 250, 1260, 1680, 40, 840, 2100, 180, 900, 270, 630, 360 })
            {
                double previous = double.NegativeInfinity;
                foreach (int rank in new[] { 0, 1, 5, 10, 100, 1000, int.MaxValue })
                {
                    double log = CultivationMath.LogPrice(price, 1.6, rank, devotion, 250);
                    check(log > previous, "Every shop's independent rank continues compounding without overflow");
                    previous = log;
                }
            }
        }
        check(HoardMath.Discount(2000, 0) == 1 && HoardMath.Discount(5000, 0) == 2.5, "Hoard discount extends smoothly past the old final tier");
        check(HoardMath.Discount(100000, 4) > HoardMath.Discount(10000, 4), "Hoard devotion benefits keep growing");
        for (int rank = 0; rank < 100; rank++)
        {
            int repaired = HoardMath.RestoredHitPoints(100, 1000, rank);
            check(repaired >= 100 && repaired <= 1000, "Recovered durability never exceeds the item's maximum");
            check(HoardMath.Restoration(rank + 1) > HoardMath.Restoration(rank), "Each restoration rank improves its repair fraction");
        }
        Console.WriteLine($"Reference Consume heat: raw {ordinary:F1}; three victims at D=2000/rank5/radius1 {at2000 * 3:F1}; twelve at D=5000/rank5/radius5 {at5000 * 12:F1}.");
    }
}
