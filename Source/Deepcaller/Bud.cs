using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Deepcaller
{
    /// <summary>
    /// Live idol-fed tuning for every Bud on the map. Curves deliberately live
    /// on the race def so later purchased upgrades can modify the same three
    /// values without replacing the weapon or projectile architecture.
    /// </summary>
    public class BudScalingExtension : DefModExtension
    {
        public SimpleCurve damageByDevotion;
        public SimpleCurve projectileSpeedFactorByDevotion;
        public SimpleCurve cooldownSecondsByDevotion;
    }

    public static class BudScalingUtility
    {
        public static BudScalingExtension ScalingFor(Thing bud) =>
            bud?.def?.GetModExtension<BudScalingExtension>();

        public static float DevotionFor(Thing bud) =>
            bud?.MapHeld == null
                ? 0f
                : CompIdolDevotion.HighestDevotion(bud.MapHeld, bud.Faction);

        public static int DamageFor(Thing bud)
        {
            var scaling = ScalingFor(bud);
            return Mathf.Max(1, (int)System.Math.Min(int.MaxValue - 1.0, System.Math.Round(
                (scaling?.damageByDevotion?.Evaluate(DevotionFor(bud)) ?? 8f)
                * ProfileFor(bud).damageFactor)));
        }

        public static float SpeedFactorFor(Thing bud)
        {
            return (float)ProfileFor(bud).speedFactor;
        }

        public static float CooldownFor(Thing bud, float fallback)
        {
            var scaling = ScalingFor(bud);
            return Mathf.Max(0.0000001f, (scaling?.cooldownSecondsByDevotion
                ?.Evaluate(DevotionFor(bud)) ?? fallback)
                / Cultivation.Factor(bud, BudUpgradeKind.FireRate));
        }

        // At most three volleys (six visual bolts) per second per Bud. Extra
        // cadence becomes payload, keeping every purchase useful at this limit.
        public static float ActualInterval(Thing bud) => (float)ProfileFor(bud).interval;
        public static float RateOverflow(Thing bud) => (float)ProfileFor(bud).rateOverflow;
        public static CultivationMath.BudProfile ProfileFor(Thing bud)
        {
            var scaling = ScalingFor(bud);
            float devotion = DevotionFor(bud);
            return CultivationMath.BudStats(scaling?.cooldownSecondsByDevotion?.Evaluate(devotion) ?? 3.2,
                scaling?.projectileSpeedFactorByDevotion?.Evaluate(devotion) ?? 1,
                Cultivation.Rank(bud, BudUpgradeKind.Damage), Cultivation.Rank(bud, BudUpgradeKind.FireRate),
                Cultivation.Rank(bud, BudUpgradeKind.BoltSpeed));
        }
    }

    public class CompProperties_BudTurretGun : CompProperties_TurretGun
    {
        public CompProperties_BudTurretGun() =>
            compClass = typeof(CompBudTurretGun);
    }

    /// <summary>
    /// CompTurretGun normally installs a fixed def-level cooldown callback.
    /// Replace only that callback per Bud so feeding the idol accelerates
    /// existing summons without mutating a shared ThingDef.
    /// </summary>
    public class CompBudTurretGun : CompTurretGun
    {
        private double shotClock;
        private double lastInterval;
        public bool EchoShot;
        public override void PostPostMake()
        {
            base.PostPostMake();
            InstallDevotionCooldown();
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref shotClock, "cultivationShotClock");
            Scribe_Values.Look(ref lastInterval, "cultivationLastInterval");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                InstallDevotionCooldown();
        }

        private void InstallDevotionCooldown()
        {
            if (GunCompEq == null)
                return;

            foreach (var verb in GunCompEq.AllVerbs)
            {
                verb.castCompleteCallback = () =>
                {
                    // Our fractional clock replaces the vanilla ten-tick poll.
                };
            }
        }

        public override void CompTick()
        {
            if (parent is not Pawn pawn || !pawn.Spawned || pawn.Dead || pawn.Downed
                || !pawn.Awake() || pawn.stances.stunner.Stunned || TurretDestroyed) return;
            AttackVerb.VerbTick();
            if (AttackVerb.state == VerbState.Bursting) return;
            double interval = BudScalingUtility.ActualInterval(parent) * 60;
            if (lastInterval > 0 && interval != lastInterval)
                shotClock *= interval / lastInterval;
            lastInterval = interval;
            if (!CultivationMath.AdvanceClock(ref shotClock, interval)) return;
            if (!currentTarget.IsValid || currentTarget.Thing == null || currentTarget.Thing.Destroyed
                || !currentTarget.Thing.Spawned || !currentTarget.Thing.HostileTo(parent)
                || currentTarget.Thing is Pawn targetPawn && (targetPawn.Dead || targetPawn.Downed)
                || !AttackVerb.CanHitTarget(currentTarget))
                currentTarget = (Thing)AttackTargetFinder.BestShootTargetFromCurrentPosition(this,
                    TargetScanFlags.NeedThreat | TargetScanFlags.NeedAutoTargetable);
            if (!currentTarget.IsValid) { shotClock = 10; return; }
            curRotation = (currentTarget.Cell.ToVector3Shifted() - parent.DrawPos).AngleFlat() + Props.angleOffset;
            if (!AttackVerb.TryStartCastOn(currentTarget, surpriseAttack: false,
                canHitNonTargetPawns: true, preventFriendlyFire: false, nonInterruptingSelfCast: true)) shotClock = 1;
        }

        public override string CompInspectStringExtra()
        {
            var speed = BudScalingUtility.SpeedFactorFor(parent);
            var cooldown = BudScalingUtility.ActualInterval(parent);
            return "Deepcaller_BudStats".Translate(
                BudScalingUtility.DamageFor(parent),
                (18f * speed).ToString("0.#"),
                cooldown.ToString("0.##")) + "\n" + "Deepcaller_BudPayload".Translate(
                    BudScalingUtility.RateOverflow(parent).ToString("0.##"),
                    Cultivation.Rank(parent, BudUpgradeKind.FireRate) >= 5 ? 2 : 1)
                + "\n" + "Deepcaller_BudSpeedPayload".Translate(BudScalingUtility.ProfileFor(parent).speedOverflow.ToString("0.##"));
        }
    }

    /// <summary>
    /// The projectile captures idol strength when launched. Its flight time
    /// and damage remain stable even if the idol feeds while it is airborne.
    /// </summary>
    [StaticConstructorOnStartup]
    public class Projectile_BudGhostfire : Bullet
    {
        private static readonly Material TailUpMaterial;
        private static readonly Material TailDownMaterial;

        private int scaledStartingTicksToImpact;
        private int dynamicDamage;
        private int launchTick;
        private int pierceTier;
        private int trailTier;

        static Projectile_BudGhostfire()
        {
            TailUpMaterial = GraphicDatabase.Get<Graphic_Single>(
                "Things/Deepcaller_Bud/GhostfireTailUp").MatSingle;
            TailDownMaterial = GraphicDatabase.Get<Graphic_Single>(
                "Things/Deepcaller_Bud/GhostfireTailDown").MatSingle;
        }

        public override int DamageAmount =>
            dynamicDamage > 0 ? dynamicDamage : base.DamageAmount;

        public override Material DrawMat =>
            ((Find.TickManager.TicksGame - launchTick) / 4 & 1) == 0
                ? TailUpMaterial
                : TailDownMaterial;

        public override Vector3 ExactPosition
        {
            get
            {
                if (scaledStartingTicksToImpact <= 0)
                    return base.ExactPosition;
                var fraction = Mathf.Clamp01(
                    1f - ticksToImpact / (float)scaledStartingTicksToImpact);
                var travel = (destination - origin).Yto0() * fraction;
                return origin.Yto0() + travel + Vector3.up * def.Altitude;
            }
        }

        public override void Launch(
            Thing launcher,
            Vector3 origin,
            LocalTargetInfo usedTarget,
            LocalTargetInfo intendedTarget,
            ProjectileHitFlags hitFlags,
            bool preventFriendlyFire = false,
            Thing equipment = null,
            ThingDef targetCoverDef = null)
        {
            double damage = BudScalingUtility.DamageFor(launcher);
            if (launcher is Pawn bud && bud.TryGetComp<CompBudTurretGun>()?.EchoShot == true)
                damage *= 0.35 + 0.05 * (Cultivation.Rank(bud, BudUpgradeKind.FireRate) / 5);
            dynamicDamage = (int)System.Math.Min(int.MaxValue - 1.0, System.Math.Max(1, System.Math.Round(damage)));
            pierceTier = Cultivation.Rank(launcher, BudUpgradeKind.Damage) / 5;
            trailTier = Cultivation.Rank(launcher, BudUpgradeKind.BoltSpeed) / 5;
            launchTick = Find.TickManager.TicksGame;
            var speedFactor = BudScalingUtility.SpeedFactorFor(launcher);
            base.Launch(launcher, origin, usedTarget, intendedTarget, hitFlags,
                preventFriendlyFire, equipment, targetCoverDef);
            scaledStartingTicksToImpact = Mathf.Max(
                1, Mathf.CeilToInt(StartingTicksToImpact / speedFactor));
            ticksToImpact = scaledStartingTicksToImpact;
            lifetime = ticksToImpact;
        }

        public override float ArmorPenetration => base.ArmorPenetration + trailTier * 0.04f;

        protected override void Tick()
        {
            base.Tick();
            if (!Destroyed && Spawned && trailTier > 0 && this.IsHashIntervalTick(4))
                FleckMaker.ThrowLightningGlow(ExactPosition, Map, 0.12f + Mathf.Min(0.2f, trailTier * 0.02f));
        }

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            var map = Map;
            var hitCell = hitThing?.Position ?? Position;
            var forward = (hitCell.ToVector3Shifted() - origin).Yto0().normalized;
            var attacker = launcher;
            bool shielded = hitThing is Pawn primary &&
                (primary.AllComps.OfType<CompShield>().Any(s => s.ShieldState == ShieldState.Active && s.Energy > 0)
                || primary.apparel?.WornApparel.Any(a => a.TryGetComp<CompShield>() is { } s
                    && s.ShieldState == ShieldState.Active && s.Energy > 0) == true);
            base.Impact(hitThing, blockedByShield);
            if (blockedByShield || shielded || pierceTier == 0 || map == null || attacker == null
                || hitThing is not Pawn) return;
            // A bounded line of secondary impacts, never extra seeking projectiles.
            int left = Mathf.Min(3, pierceTier);
            var nearby = GenRadial.RadialCellsAround(hitCell, 3.2f, true)
                .Where(c => c.InBounds(map)).SelectMany(c => c.GetThingList(map).OfType<Pawn>())
                .OrderBy(p => p.Position.DistanceToSquared(hitCell)).ToArray();
            foreach (var victim in nearby)
            {
                if (left == 0) break;
                if (victim == hitThing || victim.Dead || !victim.HostileTo(attacker)) continue;
                var delta = (victim.Position.ToVector3Shifted() - hitCell.ToVector3Shifted()).Yto0();
                float along = Vector3.Dot(delta, forward);
                if (along <= 0 || along > 3 || (delta - forward * along).magnitude > 0.8f
                    || !GenSight.LineOfSight(hitCell, victim.Position, map)) continue;
                victim.TakeDamage(new DamageInfo(DamageDefOf.Burn,
                    Mathf.Min(1000000000, dynamicDamage * (0.2f + 0.05f * pierceTier)), ArmorPenetration,
                    -1, attacker));
                left--;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref scaledStartingTicksToImpact,
                "scaledStartingTicksToImpact");
            Scribe_Values.Look(ref dynamicDamage, "dynamicDamage");
            Scribe_Values.Look(ref launchTick, "launchTick");
            Scribe_Values.Look(ref pierceTier, "pierceTier");
            Scribe_Values.Look(ref trailTier, "trailTier");
        }
    }

    public class Verb_BudGhostfire : Verb_Shoot
    {
        protected override bool TryCastShot()
        {
            var fired = base.TryCastShot();
            if (fired)
            {
                CasterPawn?.TryGetComp<CompBudAnimation>()?.BeginFire();
                var turret = CasterPawn?.TryGetComp<CompBudTurretGun>();
                if (turret != null && Cultivation.Rank(CasterPawn, BudUpgradeKind.FireRate) >= 5)
                {
                    turret.EchoShot = true;
                    try { base.TryCastShot(); }
                    finally { turret.EchoShot = false; }
                }
            }
            return fired;
        }
    }

    public class CompProperties_BudAnimation : CompProperties
    {
        public CompProperties_BudAnimation() =>
            compClass = typeof(CompBudAnimation);
    }

    public class CompBudAnimation : ThingComp
    {
        private int attackStarted = -9999;

        public int AttackTicks => Find.TickManager.TicksGame - attackStarted;

        public void BeginFire()
        {
            attackStarted = Find.TickManager.TicksGame;
            if (parent.Spawned)
                ((Pawn)parent).Drawer.renderer.SetAllGraphicsDirty();
        }

        public override void CompTick()
        {
            base.CompTick();
            if (!parent.Spawned)
                return;

            var pawn = (Pawn)parent;
            var refreshRate = AttackTicks < 24 ? 3 : 30;
            if ((Find.TickManager.TicksGame + pawn.thingIDNumber) % refreshRate == 0)
                pawn.Drawer.renderer.SetAllGraphicsDirty();
        }
    }

    [StaticConstructorOnStartup]
    public class Graphic_Bud : Graphic_Multi
    {
        private const string Root = "Things/Deepcaller_Bud/Frames/";

        private static readonly Graphic_Multi Closed;
        private static readonly Graphic_Multi Flare;
        private static readonly Graphic_Multi Recoil;

        static Graphic_Bud()
        {
            // Preload on RimWorld's main loading thread. Material selection can
            // happen on parallel render workers and must never touch ContentFinder.
            Closed = Load(Root + "Closed/BudClosed");
            Flare = Load(Root + "Flare/BudFlare");
            Recoil = Load(Root + "Recoil/BudRecoil");

            var def = DefDatabase<ThingDef>.GetNamedSilentFail("Deepcaller_Bud");
            if (def?.race != null)
                def.race.specialShadowData = null;
        }

        private static Graphic_Multi Load(string path) =>
            (Graphic_Multi)GraphicDatabase.Get<Graphic_Multi>(path);

        public override Material NodeGetMat(PawnDrawParms parms) =>
            MaterialFor(parms.pawn, parms.facing,
                base.MatAt(parms.facing, parms.pawn));

        public override Graphic GetColoredVersion(
            Shader newShader,
            Color newColor,
            Color newColorTwo) =>
            GraphicDatabase.Get<Graphic_Bud>(
                path, newShader, drawSize, newColor, newColorTwo, data, maskPath);

        public override Material MatAt(Rot4 rot, Thing thing = null)
        {
            if (!(thing is Pawn pawn))
                return base.MatAt(rot, thing);
            return MaterialFor(pawn, rot, base.MatAt(rot, thing));
        }

        public static Material MaterialFor(
            Pawn pawn,
            Rot4 rot,
            Material fallback)
        {
            var animation = pawn.TryGetComp<CompBudAnimation>();
            if (animation != null)
            {
                var ticks = animation.AttackTicks;
                if (ticks < 6)
                    return Flare.MatAt(rot, pawn);
                if (ticks < 24)
                    return Recoil.MatAt(rot, pawn);
            }

            // A brief irregular blink keeps the rooted turret alive without
            // making a supposedly static summon wobble around its cast cell.
            var cycle =
                (Find.TickManager.TicksGame + pawn.thingIDNumber * 37) % 420;
            if (cycle >= 390)
                return Closed.MatAt(rot, pawn);

            return fallback;
        }
    }

    public class JobGiver_BudWait : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            pawn.pather?.StopDead();
            var wait = JobMaker.MakeJob(JobDefOf.Wait_Combat);
            wait.expiryInterval = 600;
            wait.checkOverrideOnExpire = true;
            return wait;
        }
    }
}
