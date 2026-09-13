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
// Starting position sets acceleration; actual travel builds momentum.
Check(RiptideMath.StartForce(0, 20) == 0, "Exact center has no translational force");
Check(RiptideMath.StartForce(20, 20) == 1, "Outer edge has maximum initial pull");
Check(RiptideMath.StartForce(10, 20) == .5, "Half-radius starts at half force");
Check(RiptideMath.StartForce(20.01, 20) == 0, "Outside the AoE is never caught");
foreach (double invalid in new[] { double.NaN, double.PositiveInfinity, -1.0, 0 })
    Check(RiptideMath.StartForce(1, invalid) == 0, "Invalid radius cannot create force");
for (int start = 1; start < 20; start++)
{
    double inner = RiptideMath.StartForce(start, 20), outer = RiptideMath.StartForce(start + 1, 20);
    Check(RiptideMath.Acceleration(8000, 70, outer) > RiptideMath.Acceleration(8000, 70, inner),
        "Every outward starting cell increases acceleration");
    Check(RiptideMath.CollisionDamage(8000, 3, 8000, 70, 1, outer)
        > RiptideMath.CollisionDamage(8000, 3, 8000, 70, 1, inner), "Edge force survives the movement speed limit");
}
Check(RiptideMath.CollisionDamage(8000, 0, 8000, 70, 1, 0) == 0, "Turning in place does not generate impact damage");
for (int rank = 0; rank < 50; rank++)
{
    double before = CultivationMath.Factor(rank, .1f), after = CultivationMath.Factor(rank + 1, .1f);
    Check(RiptideMath.Acceleration(8000, 450, after) > RiptideMath.Acceleration(8000, 450, before),
        "Purchased acceleration improves vehicle pull");
    Check(RiptideMath.CollisionDamage(8000, 3, 8000, 70, 1, after)
        > RiptideMath.CollisionDamage(8000, 3, 8000, 70, 1, before), "Acceleration purchases retain impact value at one cell per tick");
}
float basePressure = RiptideMath.Pressure(8000, 0, 1);
Check(RiptideMath.ImpactDamage(basePressure, 0, 8000, 70) == 2000, "No run-up gives quarter pressure");
Check(RiptideMath.ImpactDamage(basePressure, 3, 8000, 70) == 8000, "A three-cell run-up at 8k can mine plasteel");
Check(RiptideMath.ImpactDamage(basePressure, 7, 8000, 70) == 16000, "A seven-cell run-up doubles impact force");
Check(RiptideMath.ImpactDamage(basePressure, 2, 8000, 70) < 8000, "A short run-up cannot promise a one-hit plasteel break");
foreach (double mass in new[] { .07, 10, 70, 450, 10000 })
for (int cells = 0; cells < 32; cells++)
    Check(RiptideMath.ImpactDamage(basePressure, cells + 1, 8000, mass)
        > RiptideMath.ImpactDamage(basePressure, cells, 8000, mass), "Actual travel always builds momentum");
Check(RiptideMath.ImpactDamage(100, 3, 10, 450) < RiptideMath.ImpactDamage(100, 3, 10, 70),
    "Vehicle weight reduces early impact damage");
Check(RiptideMath.ImpactDamage(100, 3, 8000, 450) > RiptideMath.ImpactDamage(100, 3, 8000, 70),
    "Vehicle mass increases late impact damage");
Check(RiptideMath.MassPullInterval(10, 450, 5) > RiptideMath.MassPullInterval(10, 70, 5),
    "Vehicles resist a weak current");
Check(RiptideMath.MassPullInterval(8000, 450, 5) < RiptideMath.MassPullInterval(10, 450, 5),
    "High devotion accelerates the vehicle");
Check(RiptideMath.ImpactDamage(basePressure, 3, 8000, .6) < RiptideMath.ImpactDamage(basePressure, 3, 8000, 70),
    "A small stack of gold is not a human-sized wrecking ball");
float Crash(double d, double mass, double run, double relativeSpeed) => RiptideMath.CollisionDamage(
    RiptideMath.Pressure(d, 0, 1), run, d, mass, relativeSpeed);
Check(Crash(8000, 70, 3, 0) == 0, "No relative motion means no crash damage");
Check(Crash(80, 450, 3, .01) < 1, "A slow vehicle nudge has less than one raw damage before armor");
Check(Crash(8000, 450, 7, 1) > Crash(8000, 450, 7, .1) * 90, "Speed has a strong effect on impact severity");
Check(Crash(8000, 70, 3, 2) == 4 * Crash(8000, 70, 3, 1), "Opposing currents hit harder than a stationary target");
Check(RiptideMath.ContactMass(450, 70, false) == RiptideMath.ContactMass(70, 450, false),
    "Contact mass does not depend on which object was processed first");
Check(RiptideMath.ContactMass(450, 1, true) == 450, "Anchored obstacles absorb the projectile's full effective mass");
Check(Crash(8000, 450, 7, RiptideMath.TravelSpeed(8000, 450, 7, 5)) >= 8000,
    "A fast heavy vehicle can mine plasteel after a run-up");
Check(Crash(8000, 120, 3, RiptideMath.TravelSpeed(8000, 120, 3, 5)) >= 8000,
    "A large animal can become a mining projectile");
Check(Crash(8000, .008, 7, 1) < 900 && Crash(1e8, .008, 7, 1) >= 900,
    "A tiny gold bar needs ungodly force to break granite");
Check(RiptideMath.Pressure(10, 0, 1) < 900, "A newly awakened idol cannot erase granite in one cast");
Check(Math.Abs(RiptideMath.Pressure(8000, 3, .4f) - 3203) < .01, "Pawn collision damage retains continuous devotion scaling");
for (int d = 10; d < 10000; d += 37)
{
    Check(RiptideMath.Pressure(d + 1, 0, 1) > RiptideMath.Pressure(d, 0, 1), "Each devotion increases mining force");
    Check(RiptideMath.PullInterval(d + 37, 4, 5) <= RiptideMath.PullInterval(d, 4, 5), "More devotion never weakens the pull");
    Check(RiptideMath.PullInterval(d, 4, 5) >= RiptideMath.PullInterval(d, 1, 5), "Heavy bodies resist more than human-sized bodies");
}
Check(RiptideMath.PullInterval(8000, 1, 5) == 1 && RiptideMath.PullInterval(8000, 4, 5) == 2,
    "An 8k idol drags a human-sized body every tick and a size-four body every two ticks");
Check(RiptideMath.ExtendedRadius(2160, 16, 2160, 16) == 16, "No radius discontinuity at the last curve point");
Check(Math.Abs(RiptideMath.ExtendedRadius(4320, 16, 2160, 16) - 18) < .001, "First devotion doubling adds two radius cells");
Check(Math.Abs(RiptideMath.ExtendedRadius(8640, 16, 2160, 16) - 20) < .001, "Second doubling still improves radius");
Check(RiptideMath.ReachExtension(4999, 0, true) == 0, "Unpurchased reach stays unchanged");
Check(RiptideMath.ReachExtension(5000, 1, true) == 2, "First area rank adds two radius cells at 5,000 devotion");
Check(RiptideMath.ReachExtension(5000, 1, false) == 4, "First distance rank adds four cast cells at 5,000 devotion");
for (int rank = 0; rank < 100; rank++)
foreach (bool area in new[] { true, false })
{
    Check(RiptideMath.ReachExtension(8000, rank + 1, area) > RiptideMath.ReachExtension(8000, rank, area),
        "Every range purchase raises the reachable limit");
    Check(RiptideMath.ReachExtension(1e100, rank + 1, area) == (rank + 1) * (area ? 4.0 : 8.0),
        "Each purchased rank has its own larger asymptote");
}
Check(RiptideMath.MapBoundedReach(32, RiptideMath.ReachExtension(8000, 10, true), 350) > 32,
    "Purchased area crosses the old free radius cap");
Check(RiptideMath.MapBoundedReach(32, RiptideMath.ReachExtension(double.MaxValue, int.MaxValue, true), 350) == 350,
    "Extreme purchased range stays within the map rather than overflowing radial tables");
foreach (double d in new[] { -1, double.NaN, double.PositiveInfinity, double.MaxValue, 1e100, 1e300 })
{
    float pressure = RiptideMath.Pressure(d, 3, .4f);
    float radius = RiptideMath.ExtendedRadius(d, 16, 2160, 16);
    Check(float.IsFinite(pressure) && pressure >= 0 && pressure <= RiptideMath.MaximumDamage, "Extreme devotion cannot overflow damage");
    Check(float.IsFinite(radius) && radius > 0 && radius <= RiptideMath.MaximumRadius, "Extreme devotion cannot overflow radial indexing");
    Check(RiptideMath.PullInterval(d, float.PositiveInfinity, 5) is >= 1 and <= 60, "Extreme devotion/mass has a finite movement cadence");
}
foreach (double mass in new[] { -1, 0, double.NaN, double.PositiveInfinity, double.MaxValue })
foreach (double devotion in new[] { -1, 0, 8000, double.NaN, double.PositiveInfinity, double.MaxValue })
{
    Check(RiptideMath.MassPullInterval(devotion, mass, 5) is >= 1 and <= 60, "Mass cadence stays within engine bounds");
    foreach (double distance in new[] { -1, 0, 7, double.NaN, double.PositiveInfinity, double.MaxValue })
    {
        float hit = RiptideMath.ImpactDamage(basePressure, distance, devotion, mass);
        Check(float.IsFinite(hit) && hit >= 0 && hit <= RiptideMath.MaximumDamage, "Extreme force inputs cannot overflow damage");
    }
}

// Each favor is independent and lapses without a sated local idol. None of
// these policy combinations can mine an empty mountain without a body.
foreach (bool sated in new[] { false, true })
foreach (int restraint in new[] { 0, 1 })
foreach (int discernment in new[] { 0, 1 })
foreach (int possessions in new[] { 0, 1 })
{
    Check(RiptideImpactRules.Resolve(false, true, false, restraint, sated) == RiptideImpact.None,
        "No body means no mining regardless of purchased favors");
    Check(RiptideImpactRules.Resolve(true, true, false, restraint, sated) == RiptideImpact.Mine,
        "An actual rock collision mines it even when structure restraint is active");
    Check(RiptideImpactRules.Resolve(true, false, true, restraint, sated)
        == (restraint == 1 && sated ? RiptideImpact.Stop : RiptideImpact.Smash), "Player walls require purchase AND satiety");
    Check(RiptideImpactRules.Resolve(true, false, false, restraint, sated) == RiptideImpact.Smash,
        "Enemy and unowned structures receive no player protection");
    Check(RiptideImpactRules.AffectsPawn(true, discernment, sated), "Hostile bodies are always eligible");
    Check(RiptideImpactRules.AffectsPawn(false, discernment, sated) == !(discernment == 1 && sated),
        "People favor controls creature pickup independently of the other two purchases");
    Check(RiptideImpactRules.AffectsPossession(true, possessions, sated) == !(possessions == 1 && sated),
        "Owned vehicles and stored baubles require their own purchase AND satiety");
    Check(RiptideImpactRules.AffectsPossession(false, possessions, sated),
        "Unclaimed chunks remain dangerous projectiles with every safety favor purchased");
    Check(RiptideImpactRules.CollisionVulnerable(false, restraint, sated),
        "People, vehicles and baubles remain collision victims under all purchase combinations");
    Check(RiptideImpactRules.CollisionVulnerable(true, restraint, sated) == !(restraint == 1 && sated),
        "Only the wall favor protects walls from impact damage");
}

// Compatibility fixtures reflect installed VPE and RimWorld of Magic defs.
// Their custom stone/metal FleshTypeDefs can claim organic flesh in 1.6.
foreach (var sample in new (bool organic, bool mechCorpse, string flesh, string race, bool nonOrganic)[] {
    (true, false, "Normal", "Human", false),
    (true, false, "Insectoid", "Megaspider", false),
    (false, true, "Mechanoid", "Mech_Centipede", true),
    (false, false, "EntityMechanical", "Nociosphere", true),
    (true, true, "CustomMetal", "ModdedRobot", true),
    (true, false, "VPE_SteelConstructFlesh", "VPE_SteelConstruct", true),
    (true, false, "VPE_RockConstructFlesh", "VPE_RockConstruct", true),
    (true, false, "TM_StoneFlesh", "TM_GraniteGolem", true),
    (true, false, "TM_MechaGolemFlesh", "TM_MechaGolem", true),
    (true, false, "TM_Golem_Flesh", "TM_WoodGolem", true),
    (true, false, "TM_Golem_Flesh", "TM_HollowGolem", true),
    (true, false, "TM_Golem_Flesh", "TM_FleshGolem", false)
})
{
    bool nonOrganic = PhysicalTargetRules.NonOrganic(sample.organic, sample.mechCorpse, sample.flesh, sample.race);
    Check(nonOrganic == sample.nonOrganic, "Classify physical material correctly: " + sample.race);
    Check(PhysicalTargetRules.BudCanAttack(nonOrganic, 0) == !sample.nonOrganic, "Organic-only Bud targeting before purchase: " + sample.race);
    Check(PhysicalTargetRules.BudCanAttack(nonOrganic, 1), "Purchased Buds target every tested material: " + sample.race);
}

// The data path must expose the physical targets to RimWorld's target picker.
DirectoryInfo? root = new DirectoryInfo(AppContext.BaseDirectory);
while (root != null && !Directory.Exists(Path.Combine(root.FullName, "1.6/Defs"))) root = root.Parent;
Check(root != null, "Find repository data for ability targeting checks");
var runtimeDefs = Directory.GetFiles(Path.Combine(root!.FullName, "1.6/Defs"), "*.xml", SearchOption.AllDirectories)
    .SelectMany(path => System.Xml.Linq.XDocument.Load(path).Root!.Elements()).ToArray();
var defNames = runtimeDefs.Select(x => (string?)x.Element("defName")).OfType<string>().ToHashSet();
foreach (var reference in runtimeDefs.SelectMany(x => x.Descendants()).Where(x => !x.HasElements
             && x.Value.Trim().StartsWith("Deepcaller_", StringComparison.Ordinal)))
    Check(defNames.Contains(reference.Value.Trim()), "Internal definition reference resolves: " + reference.Value.Trim());
var abilities = System.Xml.Linq.XDocument.Load(Path.Combine(root!.FullName, "1.6/Defs/AbilityDefs/Deepcaller.xml"));
foreach (string name in new[] { "Deepcaller_Riptide", "Deepcaller_InkVeil", "Deepcaller_Grasp" })
{
    var ability = abilities.Root!.Elements().Single(x => (string?)x.Element("defName") == name);
    var parms = ability.Element("targetingParameters")!;
    Check((string?)parms.Element("canTargetPawns") == "true", name + " accepts direct pawn targets");
    Check((string?)parms.Element("canTargetMechs") == "true", name + " accepts mech targets");
    Check((string?)parms.Element("canTargetEntities") == "true", name + " accepts entity targets");
    Check((string?)parms.Element("onlyTargetPsychicSensitive") == "false", name + " does not require a receptive mind");
    var psycast = ability.Element("modExtensions")!.Elements().Single(x => (string?)x.Attribute("Class") == "VanillaPsycastsExpanded.AbilityExtension_Psycast");
    Check((string?)psycast.Element("psychic") == "false", name + " does not scale effects to victim psychic sensitivity");
    if (name == "Deepcaller_Riptide")
    {
        Check((string?)ability.Element("requireLineOfSight") == "false", "Riptide targets through mountains");
        Check((string?)parms.Element("canTargetBuildings") == "true", "Riptide can target natural rock directly");
        Check((string?)parms.Element("canTargetSelf") == "true", "Caster can center a calamity on themselves");
        Check((string?)parms.Element("mapObjectTargetsMustBeAutoAttackable") == "false", "Natural rocks need not be combat targets");
    }
}
KineticsChecks.Run(Check);
EconomyChecks.Run(Check);
Console.WriteLine($"Passed {assertions} progression and physical-combat assertions.");
