using RimWorld;
using UnityEngine;
using Verse;

namespace Deepcaller
{
    public static class DeepcallerRenderUtility
    {
        public static bool HasIntegratedRift(Pawn pawn) =>
            pawn?.def?.defName == "Deepcaller_Tentacle"
            || pawn?.def?.defName == "Deepcaller_LeviathanArm"
            || pawn?.def?.defName == "Deepcaller_Bud"
            || pawn?.def?.defName == "Deepcaller_LeviathanHead"
            || pawn?.def?.defName == "Deepcaller_LeviathanFlower";
    }

    /// <summary>
    /// AnimalThingBase supplies a race-level shadow. Null it after defs load:
    /// the tentacle sprite family already includes its own grounded rift.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class DeepcallerSproutRenderSetup
    {
        static DeepcallerSproutRenderSetup()
        {
            var def = DefDatabase<ThingDef>.GetNamedSilentFail("Deepcaller_Tentacle");
            if (def?.race != null)
                def.race.specialShadowData = null;
            def = DefDatabase<ThingDef>.GetNamedSilentFail(
                "Deepcaller_LeviathanArm");
            if (def?.race != null)
                def.race.specialShadowData = null;
        }
    }

    public class CompProperties_SproutAnimation : CompProperties
    {
        public CompProperties_SproutAnimation() =>
            compClass = typeof(CompSproutAnimation);
    }

    /// <summary>
    /// RimWorld caches pawn render-node materials. Periodically dirtying only
    /// this pawn's graphics lets Graphic_Sprout advance its low-frequency
    /// poses without running custom draw calls every frame.
    /// </summary>
    public class CompSproutAnimation : ThingComp
    {
        private int attackAnimationStarted = -9999;
        private bool wasInCooldown;

        public int AttackAnimationTicks =>
            Find.TickManager.TicksGame - attackAnimationStarted;

        public void BeginSnapAttack()
        {
            attackAnimationStarted = Find.TickManager.TicksGame;
            if (parent.Spawned)
                ((Pawn)parent).Drawer.renderer.SetAllGraphicsDirty();
        }

        public override void CompTick()
        {
            base.CompTick();
            var pawn = (Pawn)parent;
            if (!pawn.Spawned)
                return;

            bool inCooldown = pawn.stances?.curStance is Stance_Cooldown;
            if (inCooldown && !wasInCooldown)
            {
                // The custom job begins the animation before damage. Keep that
                // original clock when its delayed hit starts the cooldown.
                if (AttackAnimationTicks >= 48)
                    attackAnimationStarted = Find.TickManager.TicksGame;
                pawn.Drawer.renderer.SetAllGraphicsDirty();
            }
            wasInCooldown = inCooldown;

            // Combat needs close sampling; ambient motion is intentionally
            // slower and cheaper.
            int refreshRate = AttackAnimationTicks < 54 ? 4 : 30;
            if ((Find.TickManager.TicksGame + pawn.thingIDNumber) % refreshRate == 0)
                pawn.Drawer.renderer.SetAllGraphicsDirty();
        }
    }

    /// <summary>
    /// Selects lightweight pose graphics for the tentacle life stages. The
    /// underlying pawn remains completely vanilla: this only changes which
    /// directional material its body node draws.
    /// </summary>
    [StaticConstructorOnStartup]
    public class Graphic_Sprout : Graphic_Multi
    {
        private const string Root = "Things/Deepcaller_Tentacle/SproutFrames/";

        private static Graphic_Multi idleA;
        private static Graphic_Multi idleB;
        private static Graphic_Multi sleepy;
        private static Graphic_Multi spiral;
        private static Graphic_Multi snap;
        private static Graphic_Multi recoil;

        static Graphic_Sprout()
        {
            // StaticConstructorOnStartup guarantees this runs on RimWorld's
            // main loading thread. Never let a parallel pawn-render task be
            // the first caller to touch ContentFinder/GraphicDatabase.
            idleA = Load(Root + "IdleA/TentacleSproutIdleA");
            idleB = Load(Root + "IdleB/TentacleSproutIdleB");
            sleepy = Load(Root + "Sleepy/TentacleSproutSleepy");
            spiral = Load(Root + "Spiral/TentacleSproutSpiral");
            snap = Load(Root + "Snap/TentacleSproutSnap");
            recoil = Load(Root + "TentacleSproutRecoil");
        }

        private static Graphic_Multi Load(string path) =>
            (Graphic_Multi)GraphicDatabase.Get<Graphic_Multi>(path);

        public override Material NodeGetMat(PawnDrawParms parms) =>
            MaterialFor(parms.pawn, parms.facing, base.MatAt(parms.facing, parms.pawn));

        /// Graphic_Multi normally returns a plain Graphic_Multi here, silently
        /// stripping this subclass when RimWorld recolors an animal graphic.
        public override Graphic GetColoredVersion(
            Shader newShader,
            Color newColor,
            Color newColorTwo) =>
            GraphicDatabase.Get<Graphic_Sprout>(
                path,
                newShader,
                drawSize,
                newColor,
                newColorTwo,
                data,
                maskPath);

        public override Material MatAt(Rot4 rot, Thing thing = null)
        {
            if (!(thing is Pawn pawn))
                return base.MatAt(rot, thing);

            return MaterialFor(pawn, rot, base.MatAt(rot, thing));
        }

        public static Material MaterialFor(Pawn pawn, Rot4 rot, Material fallback)
        {
            // Vanilla animal melee has no pre-hit warmup stance. A component
            // detects the instant cooldown begins and holds a readable visual
            // follow-through independently of the cooldown's modified length.
            var animation = pawn.TryGetComp<CompSproutAnimation>();
            if (animation != null)
            {
                int attackTicks = animation.AttackAnimationTicks;
                if (attackTicks < 18)
                    return Pose(ref spiral, Root + "Spiral/TentacleSproutSpiral", rot, pawn);
                if (attackTicks < 36)
                    return Pose(ref snap, Root + "Snap/TentacleSproutSnap", rot, pawn);
                if (attackTicks < 54)
                    return Pose(ref recoil, Root + "TentacleSproutRecoil", rot, pawn);
            }

            // Alert Sprouts never fall asleep while tracking an enemy.
            if (pawn.mindState?.enemyTarget != null)
                return fallback;

            // Per-pawn phase prevents a summoned group from moving in lockstep.
            int cycle = (Find.TickManager.TicksGame + pawn.thingIDNumber * 37) % 1800;
            if (cycle >= 1540 && cycle < 1720)
                return Pose(ref sleepy, Root + "Sleepy/TentacleSproutSleepy", rot, pawn);

            // A slow two-frame sway also makes the integrated rift edge pulse.
            int wobble = (Find.TickManager.TicksGame + pawn.thingIDNumber * 11) / 60;
            if ((wobble & 3) == 1)
                return Pose(ref idleA, Root + "IdleA/TentacleSproutIdleA", rot, pawn);
            if ((wobble & 3) == 3)
                return Pose(ref idleB, Root + "IdleB/TentacleSproutIdleB", rot, pawn);

            return fallback;
        }

        private static Material Pose(
            ref Graphic_Multi cache,
            string path,
            Rot4 rot,
            Thing thing)
        {
            if (cache == null)
                Log.ErrorOnce($"[Deepcaller] Sprout pose was not preloaded: {path}",
                    path.GetHashCode());

            return cache?.MatAt(rot, thing) ?? BaseContent.BadMat;
        }
    }
}
