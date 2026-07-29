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
            return Mathf.Max(1, Mathf.RoundToInt(
                scaling?.damageByDevotion?.Evaluate(DevotionFor(bud)) ?? 8f));
        }

        public static float SpeedFactorFor(Thing bud)
        {
            var scaling = ScalingFor(bud);
            return Mathf.Max(0.1f,
                scaling?.projectileSpeedFactorByDevotion?.Evaluate(DevotionFor(bud)) ?? 1f);
        }

        public static float CooldownFor(Thing bud, float fallback)
        {
            var scaling = ScalingFor(bud);
            return Mathf.Max(0.1f,
                scaling?.cooldownSecondsByDevotion?.Evaluate(DevotionFor(bud)) ?? fallback);
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
        public override void PostPostMake()
        {
            base.PostPostMake();
            InstallDevotionCooldown();
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
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
                    var fallback = AttackVerb.verbProps.defaultCooldownTime;
                    burstCooldownTicksLeft =
                        BudScalingUtility.CooldownFor(parent, fallback).SecondsToTicks();
                };
            }
        }

        public override string CompInspectStringExtra()
        {
            var devotion = BudScalingUtility.DevotionFor(parent);
            var speed = parent.def.GetModExtension<BudScalingExtension>()
                ?.projectileSpeedFactorByDevotion?.Evaluate(devotion) ?? 1f;
            var cooldown = BudScalingUtility.CooldownFor(
                parent, AttackVerb.verbProps.defaultCooldownTime);
            return "Deepcaller_BudStats".Translate(
                BudScalingUtility.DamageFor(parent),
                (18f * speed).ToString("0.#"),
                cooldown.ToString("0.##"));
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
            dynamicDamage = BudScalingUtility.DamageFor(launcher);
            launchTick = Find.TickManager.TicksGame;
            var speedFactor = BudScalingUtility.SpeedFactorFor(launcher);
            base.Launch(launcher, origin, usedTarget, intendedTarget, hitFlags,
                preventFriendlyFire, equipment, targetCoverDef);
            scaledStartingTicksToImpact = Mathf.Max(
                1, Mathf.CeilToInt(StartingTicksToImpact / speedFactor));
            ticksToImpact = scaledStartingTicksToImpact;
            lifetime = ticksToImpact;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref scaledStartingTicksToImpact,
                "scaledStartingTicksToImpact");
            Scribe_Values.Look(ref dynamicDamage, "dynamicDamage");
            Scribe_Values.Look(ref launchTick, "launchTick");
        }
    }

    public class Verb_BudGhostfire : Verb_Shoot
    {
        protected override bool TryCastShot()
        {
            var fired = base.TryCastShot();
            if (fired)
                CasterPawn?.TryGetComp<CompBudAnimation>()?.BeginFire();
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
