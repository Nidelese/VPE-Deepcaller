using Deepcaller;

int assertions = 0;
void Check(bool condition, string message)
{
    assertions++;
    if (!condition) throw new Exception(message);
}
int Price(int rank, double devotion) => CultivationMath.TryPrice(
    CultivationMath.LogPrice(25, 1.6, rank, devotion, 250), out int cost) ? cost : -1;

// Exact known player-facing prices and conversion boundaries.
Check(Price(0, 0) == 25, "First fire-rate rank costs 25 gold");
Check(Price(1, 0) == 40, "First compounding step must not round up due to float widening");
Check(Price(2, 0) == 64, "Second exact compounding step");
Check(Price(0, 250) == 13, "Round half-price upward");
Check(Price(10, 2500) == 250, "Existing midgame quote");
Check(Price(20, 100000) == 754, "Existing late-game quote");
Check(Price(0, 1e100) == 1, "Keep a one-gold floor");
Check(Price(44, 2500) == -1, "Out-of-range prices must be unavailable, never cheap");
Check(Price(183, 1e40) > 0, "Extreme discount still rescues a rank whose float numerator overflowed");
Check(CultivationMath.TryPrice(Math.Log(int.MaxValue), out int largest) && largest == int.MaxValue,
    "Largest representable price");
Check(!CultivationMath.TryPrice(double.PositiveInfinity, out _), "Infinity cannot be purchased");
Check(!CultivationMath.TryPrice(double.NaN, out _), "NaN cannot be purchased");
Check(Price(int.MaxValue, double.MaxValue) == -1, "Extreme rank fails closed without overflow");
for (int rank = 0; rank < 250; rank++)
{
    double before = CultivationMath.LogPrice(25, 1.6, rank, 100000, 250);
    Check(CultivationMath.LogPrice(25, 1.6, rank + 1, 100000, 250) > before,
        "Every rank compounds even far beyond integer prices");
    Check(CultivationMath.LogPrice(25, 1.6, rank, 200000, 250) < before,
        "Every devotion increase lowers the underlying price");
}
for (int rank = 0; rank < 25; rank++)
{
    double goal = CultivationMath.DevotionNeeded(25, 1.6, rank, 10, 250);
    Check(Price(rank, goal + 0.001) <= 10, "Pinned goal makes purchase affordable");
    if (goal > 1) Check(Price(rank, goal - 1) > 10, "Pinned goal does not overpromise");
}
Check(double.IsPositiveInfinity(CultivationMath.DevotionNeeded(25, 1.6, 10, 0, 250)), "No zero-gold purchase");

// Test the production clock against expected elapsed time, including fractional
// intervals that the vanilla ten-tick scheduler used to discard.
foreach (double interval in new[] { 20.0, 20.25, 23.73, 27.0, 192.0 })
{
    double clock = 0;
    int shots = 0;
    for (int tick = 0; tick < 60000; tick++) if (CultivationMath.AdvanceClock(ref clock, interval)) shots++;
    Check(Math.Abs(shots - 60000 / interval) <= 1.01, "Fractional cadence stays within one volley over 60,000 ticks");
}
double maxShotDebt = -10000;
Check(CultivationMath.AdvanceClock(ref maxShotDebt, 20) && maxShotDebt >= 19,
    "Idle time must not bank an unbounded burst");

// Each rank must buy either real cadence/speed or payload. Visual projectile
// output remains bounded while effective damage continues increasing.
foreach (double baseInterval in new[] { 3.2, 2.8, 0.9, 0.45 })
for (int rank = 0; rank < 250; rank++)
{
    var current = CultivationMath.BudStats(baseInterval, 4, 0, rank, 0);
    var next = CultivationMath.BudStats(baseInterval, 4, 0, rank + 1, 0);
    double dps = current.damageFactor * (1 + current.echoFactor) / current.interval;
    double nextDps = next.damageFactor * (1 + next.echoFactor) / next.interval;
    Check(nextDps > dps, "Every fire-rate rank improves effective DPS");
    Check(next.interval >= 1.0 / 3, "No more than three volleys per second");
    var speed = CultivationMath.BudStats(baseInterval, 4, 0, 0, rank);
    var nextSpeed = CultivationMath.BudStats(baseInterval, 4, 0, 0, rank + 1);
    Check(nextSpeed.speedFactor > speed.speedFactor || nextSpeed.damageFactor > speed.damageFactor,
        "Speed rank must improve flight or impact");
    Check(nextSpeed.speedFactor <= 10, "Physical speed stays bounded");
}
Check(CultivationMath.BudStats(.45, 4, 0, 4, 0).echoFactor == 0, "No echo before milestone");
Check(CultivationMath.BudStats(.45, 4, 0, 5, 0).echoFactor > 0, "Echo unlocks at fifth rank");

// Save migration retains earned forms without erasing additional feeding.
float[] thresholds = { 2, 6, 15 };
Check(CultivationMath.ReconcileGrowth(1.5f, 1, thresholds) == 2, "Old Grasper stays earned");
Check(CultivationMath.ReconcileGrowth(4, 2, thresholds) == 6, "Old Crusher stays earned");
Check(CultivationMath.ReconcileGrowth(8, 3, thresholds) == 15, "Old Colossus stays earned");
Check(CultivationMath.ReconcileGrowth(25, 3, thresholds) == 25, "Preserve excess growth");
Check(CultivationMath.ReconcileGrowth(1, 0, thresholds) == 1, "Do not grant unearned stages");

// Multiple attackers share one lifetime budget; healing prey cannot reset it.
float credited = 0;
Check(CultivationMath.CreditDamage(60, 100, ref credited) == 60, "First attacker contribution");
Check(CultivationMath.CreditDamage(90, 100, ref credited) == 40, "Second attacker limited by remainder");
Check(CultivationMath.CreditDamage(100, 100, ref credited) == 0, "No repeated growth from healed prey");
Check(CultivationMath.CreditDamage(-10, 100, ref credited) == 0 && credited == 100, "Invalid damage cannot reset budget");
Check(CultivationMath.LifetimeGain(2500, 16000, 29500, 30000) == 500, "Lifetime cap clips last meal");
Check(CultivationMath.LifetimeGain(2500, 16000, 30000, 30000) == 0, "No immortal feeding loop");
Check(CultivationMath.LifetimeGain(2500, 1000, 0, 30000) == 1000, "Never extend beyond original remaining duration");
Check(CultivationMath.IdolTier(9) == 0 && CultivationMath.IdolTier(10) == 1, "First awakening boundary");
Check(CultivationMath.IdolTier(2000) == 7, "Final named awakening");
Check(CultivationMath.NextIdolMilestone(2000) == 4000 && CultivationMath.NextIdolMilestone(4000) == 8000,
    "Further depths retain a next milestone");
Console.WriteLine($"Passed {assertions} progression assertions: prices, goals, clocks, useful ranks, migration, feeding budgets and milestones.");
