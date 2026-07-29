using RimWorld;
using UnityEngine;
using Verse;

namespace Deepcaller
{
    /// <summary>
    /// Lightweight expression clock shared by the Leviathan's head and its
    /// three flowers. The pawns and their targeting remain independent; this
    /// component only tells their graphics when their own weapon fired.
    /// </summary>
    public class CompProperties_LeviathanAnimation : CompProperties
    {
        public CompProperties_LeviathanAnimation() =>
            compClass = typeof(CompLeviathanAnimation);
    }

    public class CompLeviathanAnimation : ThingComp
    {
        private int attackStarted = -9999;

        public int AttackTicks => Find.TickManager.TicksGame - attackStarted;

        public void BeginFire()
        {
            attackStarted = Find.TickManager.TicksGame;
            DirtyGraphics();
        }

        public override void CompTick()
        {
            base.CompTick();
            if (!parent.Spawned)
                return;

            // Sample combat expressions closely, but idle blinks only need a
            // low-frequency refresh. Per-pawn phase prevents synchronized eyes.
            int refreshRate = AttackTicks < 28 ? 3 : 20;
            if ((Find.TickManager.TicksGame + parent.thingIDNumber) % refreshRate == 0)
                DirtyGraphics();
        }

        private void DirtyGraphics()
        {
            if (parent is Pawn pawn && pawn.Spawned)
                pawn.Drawer.renderer.SetAllGraphicsDirty();
        }
    }

    public class Verb_LeviathanGlob : Verb_Shoot
    {
        protected override bool TryCastShot()
        {
            bool fired = base.TryCastShot();
            if (fired)
                CasterPawn?.TryGetComp<CompLeviathanAnimation>()?.BeginFire();
            return fired;
        }
    }

    public class Verb_LeviathanFlowerStun : Verb_Shoot
    {
        protected override bool TryCastShot()
        {
            bool fired = base.TryCastShot();
            if (fired)
                CasterPawn?.TryGetComp<CompLeviathanAnimation>()?.BeginFire();
            return fired;
        }
    }

    [StaticConstructorOnStartup]
    public class Graphic_Leviathan : Graphic_Multi
    {
        private const string Root =
            "Things/Deepcaller_Leviathan/HeadFrames/";

        private static readonly Graphic_Multi Blink;
        private static readonly Graphic_Multi Focus;
        private static readonly Graphic_Multi Pleased;

        static Graphic_Leviathan()
        {
            // Preload on the main loading thread. Pawn render workers may call
            // MaterialFor and must not be the first to touch GraphicDatabase.
            Blink = Load(Root + "Blink/LeviathanHeadBlink");
            Focus = Load(Root + "Focus/LeviathanHeadFocus");
            Pleased = Load(Root + "Pleased/LeviathanHeadPleased");

            var def = DefDatabase<ThingDef>.GetNamedSilentFail(
                "Deepcaller_LeviathanHead");
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
            GraphicDatabase.Get<Graphic_Leviathan>(
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
            var animation = pawn.TryGetComp<CompLeviathanAnimation>();
            if (animation != null)
            {
                int ticks = animation.AttackTicks;
                if (ticks < 7)
                    return Focus.MatAt(rot, pawn);
                if (ticks < 28)
                    return Pleased.MatAt(rot, pawn);
            }

            // A rare, soft blink makes the enormous face feel calm and alive.
            int cycle =
                (Find.TickManager.TicksGame + pawn.thingIDNumber * 47) % 520;
            if (cycle >= 490)
                return Blink.MatAt(rot, pawn);

            return fallback;
        }
    }

    [StaticConstructorOnStartup]
    public class Graphic_LeviathanFlower : Graphic_Multi
    {
        private const string Root =
            "Things/Deepcaller_Leviathan/FlowerFrames/";

        private static readonly Graphic_Multi Blink;
        private static readonly Graphic_Multi Focus;

        static Graphic_LeviathanFlower()
        {
            Blink = Load(Root + "Blink/LeviathanFlowerBlink");
            Focus = Load(Root + "Focus/LeviathanFlowerFocus");

            var def = DefDatabase<ThingDef>.GetNamedSilentFail(
                "Deepcaller_LeviathanFlower");
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
            GraphicDatabase.Get<Graphic_LeviathanFlower>(
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
            var animation = pawn.TryGetComp<CompLeviathanAnimation>();
            if (animation != null && animation.AttackTicks < 16)
                return Focus.MatAt(rot, pawn);

            // Different prime-ish offset and cycle from the head keeps all
            // four prominent eyes from blinking as a single mechanical unit.
            int cycle =
                (Find.TickManager.TicksGame + pawn.thingIDNumber * 61) % 430;
            if (cycle >= 402)
                return Blink.MatAt(rot, pawn);

            return fallback;
        }
    }
}
