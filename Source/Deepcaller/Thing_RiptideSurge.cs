using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Deepcaller
{
    public class RiptideBody : IExposable
    {
        public Thing thing;
        public double mass, distance, startForce = 1;
        public CurrentVector position, velocity, startingDirection;
        public bool directTarget = true, crossed, initialized, simulating, finished, started;
        public UnityEngine.Vector3 observedDrawPosition;
        public bool observed;
        public bool pickupAllowed;

        public void Initialize(IntVec3 center)
        {
            position = new CurrentVector(thing.Position.x, thing.Position.z);
            startingDirection = (new CurrentVector(center.x, center.z) - position).Unit;
            crossed = startingDirection.Length == 0;
            initialized = true;
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref thing, "body");
            Scribe_Values.Look(ref mass, "mass", 70.0);
            Scribe_Values.Look(ref distance, "distance", 0.0);
            Scribe_Values.Look(ref startForce, "startForce", 1.0);
            Scribe_Values.Look(ref directTarget, "directTarget", true);
            Scribe_Values.Look(ref crossed, "crossed");
            Scribe_Values.Look(ref initialized, "kineticInitialized");
            Scribe_Values.Look(ref simulating, "simulating");
            Scribe_Values.Look(ref position.x, "positionX");
            Scribe_Values.Look(ref position.z, "positionZ");
            Scribe_Values.Look(ref velocity.x, "velocityX");
            Scribe_Values.Look(ref velocity.z, "velocityZ");
            Scribe_Values.Look(ref startingDirection.x, "directionX");
            Scribe_Values.Look(ref startingDirection.z, "directionZ");
            Scribe_Values.Look(ref finished, "finished");
            Scribe_Values.Look(ref started, "started");
        }
    }

    // One fixed roster captures both direct targets and potential collateral.
    // A collider may receive momentum without receiving the god's acceleration.
    public class Thing_RiptideSurge : Thing
    {
        private Pawn caster;
        private double devotion, accelerationMultiplier = 1;
        private float collisionDamage, wallDamage;
        private int baseTicks = 5, stunTicks = 120, age;
        private List<RiptideBody> bodies = new List<RiptideBody>();
        private List<Thing> collisionRoster = new List<Thing>();
        private HashSet<Thing> collidable;
        private Dictionary<Thing, RiptideBody> bodyLookup;
        private Dictionary<Thing, float> debrisWear = new Dictionary<Thing, float>();
        private List<Thing> wearKeys;
        private List<float> wearValues;
        private Thing_RiptideSurge[] otherSurges;
        private readonly HashSet<long> contacts = new HashSet<long>();
        private CurrentVector Center => new CurrentVector(Position.x, Position.z);

        public bool Captured(Thing thing) => (collidable ??= new HashSet<Thing>(collisionRoster)).Contains(thing);

        public static double MassOf(Thing thing)
        {
            double mass = thing is Pawn vehicle && RiptideVehicleBridge.IsVehicle(vehicle)
                ? RiptideVehicleBridge.Mass(vehicle) : thing.GetStatValue(StatDefOf.Mass) * thing.stackCount;
            if (mass <= 0 && thing is Pawn pawn) mass = pawn.BodySize * 70;
            return RiptideMath.Mass(mass);
        }

        private void EnsureLookup()
        {
            collidable ??= new HashSet<Thing>(collisionRoster);
            bodyLookup ??= bodies.Where(b => b.thing != null).GroupBy(b => b.thing).ToDictionary(g => g.Key, g => g.First());
        }

        private static IEnumerable<Thing_RiptideSurge> Surges(Map map) =>
            map.listerThings.ThingsOfDef(DefDatabase<ThingDef>.GetNamed("Deepcaller_RiptideSurge")).OfType<Thing_RiptideSurge>();

        private RiptideBody OtherDriver(Thing thing)
        {
            foreach (var surge in otherSurges ?? Array.Empty<Thing_RiptideSurge>())
            {
                if (surge == this || surge.Destroyed) continue;
                surge.EnsureLookup();
                if (surge.bodyLookup.TryGetValue(thing, out var body) && body.simulating) return body;
            }
            return null;
        }

        public static void Spawn(Map map, IntVec3 center, Pawn caster, double devotion,
            AbilityExtension_Riptide ext, float radius, List<Thing> snapshot, List<Thing> collisionSnapshot)
        {
            if (snapshot.Count == 0) return;
            var ids = new HashSet<Thing>(snapshot);
            var inheritedVelocity = new Dictionary<Thing, CurrentVector>();
            // A redirect keeps real inertia but replaces the old acceleration.
            foreach (var old in Surges(map))
                foreach (var body in old.bodies)
                    if (body.thing != null && ids.Contains(body.thing))
                    {
                        if (body.simulating) inheritedVelocity[body.thing] = body.velocity;
                        body.directTarget = body.simulating = false;
                        body.finished = true;
                    }

            var surge = (Thing_RiptideSurge)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Deepcaller_RiptideSurge"));
            surge.caster = caster;
            surge.devotion = devotion;
            surge.accelerationMultiplier = Cultivation.Factor(caster, BudUpgradeKind.RiptideAcceleration, 0.1f);
            surge.collisionDamage = RiptideMath.Pressure(devotion, ext.collisionDamageBase, ext.collisionDamagePerDevotion);
            surge.wallDamage = RiptideMath.Pressure(devotion, ext.wallDamageBase, ext.wallDamagePerDevotion);
            surge.baseTicks = ext.ticksPerCell;
            surge.stunTicks = ext.stunTicks;
            surge.collisionRoster = collisionSnapshot.Distinct().ToList();
            foreach (var thing in collisionSnapshot.Concat(snapshot).Distinct().Where(RiptideImpactUtility.IsMovable))
            {
                var body = new RiptideBody { thing = thing, mass = MassOf(thing),
                    directTarget = ids.Contains(thing),
                    startForce = RiptideMath.StartForce(thing.Position.DistanceTo(center), radius) };
                body.Initialize(center);
                if (inheritedVelocity.TryGetValue(thing, out var velocity))
                { body.velocity = velocity; body.simulating = velocity.Length > RiptideKinetics.RestSpeed; }
                surge.bodies.Add(body);
                if (body.directTarget && thing is Pawn victim)
                {
                    var legacy = victim.health?.hediffSet.GetFirstHediffOfDef(Deepcaller_DefOf.Deepcaller_Riptiden);
                    if (legacy != null) legacy.Severity = 0;
                }
            }
            GenSpawn.Spawn(surge, center, map);
        }

        private bool Valid(RiptideBody body) => RiptideImpactUtility.IsMovable(body.thing) && body.thing.Map == Map;
        private bool Driven(RiptideBody body) => body.directTarget && !body.finished && !body.crossed
            && body.pickupAllowed;
        private double Acceleration(RiptideBody body) => RiptideKinetics.Acceleration(devotion, body.mass,
            body.startForce * accelerationMultiplier, baseTicks);
        private static IntVec3 Cell(CurrentVector point) => new IntVec3(
            (int)Math.Floor(point.x + 0.5), 0, (int)Math.Floor(point.z + 0.5));

        protected override void Tick()
        {
            if (++age > 3600 || bodies.Count == 0) { Destroy(); return; }
            EnsureLookup();
            otherSurges = Surges(Map).Where(s => s != this).ToArray();
            contacts.Clear();
            // Observe ordinary walking/vehicle motion before applying impulses.
            foreach (var body in bodies)
            {
                if (!Valid(body)) { body.simulating = false; body.finished = true; continue; }
                body.pickupAllowed = body.directTarget && RiptideImpactUtility.AffectsThing(body.thing, caster);
                if (!body.initialized) body.Initialize(Position);
                if (!body.simulating)
                {
                    var draw = body.thing.DrawPos;
                    if (body.observed)
                        body.velocity = RiptideKinetics.Bounded(new CurrentVector(
                            draw.x - body.observedDrawPosition.x, draw.z - body.observedDrawPosition.z));
                    body.observedDrawPosition = draw;
                    body.observed = true;
                    body.position = new CurrentVector(body.thing.Position.x, body.thing.Position.z);
                }
            }
            foreach (var body in bodies)
            {
                if (!Valid(body)) continue;
                if (OtherDriver(body.thing) != null && !body.simulating) continue;
                if (body.directTarget && !body.finished && !body.started
                    && body.pickupAllowed)
                {
                    if (body.startForce == 0 && body.velocity.Length <= RiptideKinetics.RestSpeed)
                    { RiptideVehicleBridge.TurnAround(body.thing); body.finished = true; continue; }
                    body.simulating = true;
                    body.started = true;
                }
                if (!body.simulating) continue;
                if (body.thing is Pawn victim)
                {
                    victim.jobs?.StopAll();
                    RiptideVehicleBridge.Halt(victim);
                    victim.stances?.stunner?.StunFor(2, caster, addBattleLog: false);
                }
                Move(body);
            }
            if (!bodies.Any(b => Valid(b) && (b.simulating || Driven(b)))) Destroy();
        }

        private void Move(RiptideBody body)
        {
            double acceleration = Acceleration(body);
            double maximum = body.velocity.Length + (Driven(body) ? acceleration : 0);
            int steps = Math.Max(1, Math.Min(32, (int)Math.Ceiling(maximum * 4)));
            for (int step = 0; step < steps && body.simulating && Valid(body); step++)
            {
                bool driven = Driven(body);
                var nextPosition = body.position;
                var nextVelocity = body.velocity;
                bool crossed = body.crossed;
                RiptideKinetics.Advance(ref nextPosition, ref nextVelocity, ref crossed, Center,
                    body.startingDirection, acceleration, body.mass, 1.0 / steps, driven);
                body.velocity = nextVelocity;
                var nextCell = Cell(nextPosition);
                if (nextCell != body.thing.Position && !EnterCell(body, nextCell, driven)) continue;
                if (!Valid(body)) break;
                body.distance += (nextPosition - body.position).Length;
                body.position = nextPosition;
                body.crossed = crossed;
                if (body.thing.Position != nextCell)
                {
                    body.thing.Position = nextCell;
                    if (body.thing is Pawn pawn) RiptideVehicleBridge.NotifyMoved(pawn);
                }
            }
            if (Valid(body) && !Driven(body) && body.velocity.Length <= RiptideKinetics.RestSpeed) Finish(body);
        }

        private bool EnterCell(RiptideBody body, IntVec3 next, bool driven)
        {
            var thing = body.thing;
            var cells = RiptideVehicleBridge.ProjectedCells(thing, next).ToArray();
            if (cells.Any(c => !c.InBounds(Map))) { Finish(body); return false; }
            var previous = new HashSet<IntVec3>(RiptideVehicleBridge.ProjectedCells(thing, thing.Position));
            var swept = new HashSet<IntVec3>(cells);
            // Include corner contacts; fast diagonals cannot tunnel between walls.
            if (next.x != thing.Position.x && next.z != thing.Position.z)
            {
                swept.UnionWith(RiptideVehicleBridge.ProjectedCells(thing, new IntVec3(next.x, 0, thing.Position.z)));
                swept.UnionWith(RiptideVehicleBridge.ProjectedCells(thing, new IntVec3(thing.Position.x, 0, next.z)));
            }
            var obstacles = swept.Where(c => c.InBounds(Map) && !previous.Contains(c)).SelectMany(c => c.GetThingList(Map))
                .Where(t => t != thing && Captured(t) && !t.Destroyed
                    && (t is not Building_Door door || !door.Open)).Distinct().ToArray();
            foreach (var obstacle in obstacles)
            {
                if (!Valid(body)) return false;
                bool barrier = obstacle is Building;
                bool anchored = barrier || obstacle is Plant;
                bodyLookup.TryGetValue(obstacle, out var localOther);
                var other = OtherDriver(obstacle) ?? localOther;
                CurrentVector otherVelocity = other?.velocity ?? default;
                var normal = new CurrentVector(next.x - thing.Position.x, next.z - thing.Position.z).Unit;
                double closing = (body.velocity - otherVelocity).Dot(normal);
                bool protectedWall = barrier && RiptideImpactUtility.Resolve(thing, obstacle, caster) == RiptideImpact.Stop;
                if (protectedWall)
                {
                    Damage(thing, RiptideKinetics.Damage(collisionDamage, body.mass, Math.Max(0, closing)));
                    Finish(body);
                    return false;
                }
                long pair = ((long)Math.Min(thing.thingIDNumber, obstacle.thingIDNumber) << 32)
                    | (uint)Math.Max(thing.thingIDNumber, obstacle.thingIDNumber);
                if (closing > 1e-6 && contacts.Add(pair))
                {
                    double mass = RiptideMath.ContactMass(body.mass, other?.mass ?? (anchored ? 0 : MassOf(obstacle)), anchored);
                    float damage = RiptideKinetics.Damage(collisionDamage, mass, closing);
                    if (barrier) RiptideImpactUtility.Apply(thing, obstacle, RiptideKinetics.Damage(wallDamage, mass, closing), caster);
                    else Damage(obstacle, damage);
                    Damage(thing, damage);
                    var incoming = body.velocity;
                    bool otherDriven = other != null && other.directTarget && !other.crossed && !other.finished
                        && RiptideImpactUtility.AffectsThing(obstacle, caster);
                    RiptideKinetics.Collide(ref incoming, ref otherVelocity, normal, body.mass,
                        other?.mass ?? 0, anchored, driven, otherDriven);
                    body.velocity = incoming;
                    if (other != null && RiptideImpactUtility.IsMovable(obstacle))
                    {
                        other.velocity = otherVelocity;
                        other.simulating = true;
                        // A spared/outside target is moved by impact, not enrolled
                        // in the god's pull. Its pickup favor is still honored.
                        if (other == localOther && !other.initialized) other.Initialize(Position);
                    }
                }
                if (!Valid(body)) return false;
                if (!obstacle.Destroyed && obstacle.Spawned && (anchored
                    || (RiptideVehicleBridge.IsVehicle(thing) || RiptideVehicleBridge.IsVehicle(obstacle))
                        && RiptideVehicleBridge.ProjectedCells(obstacle, obstacle.Position).Any(cells.Contains)))
                {
                    // Never occupy intact rock or overwrite a surviving vehicle's
                    // grid claim. Before center, stored velocity keeps building.
                    return false;
                }
            }
            return Valid(body);
        }

        private void Damage(Thing target, float amount)
        {
            if (target == null || !target.Spawned || target.Destroyed || target.Map != Map || amount <= 0) return;
            var hit = new DamageInfo(DamageDefOf.Blunt, amount, 0, -1, caster);
            target.TakeDamage(hit);
            if (!target.Destroyed && target.def.category == ThingCategory.Item
                && !target.def.useHitPoints && target.def.destroyable)
            {
                debrisWear.TryGetValue(target, out float wear);
                debrisWear[target] = wear += amount;
                if (wear >= Math.Max(1, target.MaxHitPoints)) target.Kill(hit);
            }
        }

        private void Finish(RiptideBody body)
        {
            double speed = body.velocity.Length;
            body.velocity = default;
            body.simulating = false;
            body.finished = true;
            if (Valid(body)) body.observedDrawPosition = body.thing.DrawPos;
            if (Valid(body) && body.thing is Pawn pawn && speed > RiptideKinetics.RestSpeed)
                pawn.stances?.stunner?.StunFor((int)Math.Ceiling(stunTicks * Math.Min(1, speed)), caster, addBattleLog: false);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                bodies.RemoveAll(b => b.thing == null || b.thing.Destroyed || !b.thing.Spawned);
                collisionRoster.RemoveAll(t => t == null || t.Destroyed || !t.Spawned);
                foreach (var key in debrisWear.Keys.Where(t => t == null || t.Destroyed || !t.Spawned).ToArray())
                    debrisWear.Remove(key);
            }
            Scribe_References.Look(ref caster, "caster");
            Scribe_Values.Look(ref devotion, "devotion", 0.0);
            Scribe_Values.Look(ref accelerationMultiplier, "accelerationMultiplier", 1.0);
            Scribe_Values.Look(ref collisionDamage, "collisionDamage");
            Scribe_Values.Look(ref wallDamage, "wallDamage");
            Scribe_Values.Look(ref baseTicks, "baseTicks", 5);
            Scribe_Values.Look(ref stunTicks, "stunTicks", 120);
            Scribe_Values.Look(ref age, "age");
            Scribe_Collections.Look(ref bodies, "bodies", LookMode.Deep);
            Scribe_Collections.Look(ref collisionRoster, "collisionRoster", LookMode.Reference);
            Scribe_Collections.Look(ref debrisWear, "debrisWear", LookMode.Reference, LookMode.Value, ref wearKeys, ref wearValues);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                bodies ??= new List<RiptideBody>();
                collisionRoster ??= new List<Thing>();
                debrisWear ??= new Dictionary<Thing, float>();
                bodyLookup = null;
                collidable = null;
            }
        }
    }
}
