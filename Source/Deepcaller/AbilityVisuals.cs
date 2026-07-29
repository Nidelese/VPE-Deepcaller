using RimWorld;
using UnityEngine;
using Verse;

namespace Deepcaller
{
    public enum DeepcallerVisualKind
    {
        Riptide,
        Grasp,
        GraspRelease,
        Ink,
        ShieldBreak
    }

    /// <summary>
    /// A saved, non-interactive visual actor for Deepcaller abilities.
    /// Mechanics remain on the abilities and hediffs; this Thing only gives
    /// those mechanics a coherent animated body.
    /// </summary>
    public class Thing_AbilityVisual : Thing
    {
        private DeepcallerVisualKind kind;
        private int ageTicks;
        private int lifespanTicks = 60;
        private float radius = 1f;
        private Thing target;
        private int seed;
        private int lastPulseTick = -9999;

        public static Thing_AbilityVisual SpawnRiptide(
            Map map, IntVec3 center, float radius, int lifespanTicks)
        {
            return Spawn(DeepcallerVisualKind.Riptide, map, center,
                Mathf.Max(1, lifespanTicks), radius);
        }

        public static Thing_AbilityVisual SpawnGrasp(
            Map map, IntVec3 anchor, Thing target, int lifespanTicks)
        {
            return Spawn(DeepcallerVisualKind.Grasp, map, anchor,
                Mathf.Max(1, lifespanTicks), 1f, target);
        }

        public static Thing_AbilityVisual SpawnGraspRelease(Map map, IntVec3 cell)
        {
            return Spawn(DeepcallerVisualKind.GraspRelease, map, cell, 30, 1f);
        }

        public static Thing_AbilityVisual SpawnInk(
            Map map, IntVec3 center, float radius, int lifespanTicks)
        {
            return Spawn(DeepcallerVisualKind.Ink, map, center,
                Mathf.Max(1, lifespanTicks), radius);
        }

        public static Thing_AbilityVisual SpawnShieldBreak(Map map, IntVec3 cell, float scale)
        {
            return Spawn(DeepcallerVisualKind.ShieldBreak, map, cell, 24, scale);
        }

        private static Thing_AbilityVisual Spawn(
            DeepcallerVisualKind kind, Map map, IntVec3 cell,
            int lifespanTicks, float radius, Thing target = null)
        {
            if (map == null || !cell.IsValid || !cell.InBounds(map)
                || Deepcaller_DefOf.Deepcaller_AbilityVisual == null)
                return null;

            var visual = (Thing_AbilityVisual)ThingMaker.MakeThing(
                Deepcaller_DefOf.Deepcaller_AbilityVisual);
            visual.kind = kind;
            visual.lifespanTicks = lifespanTicks;
            visual.radius = radius;
            visual.target = target;
            visual.seed = Rand.Int;
            GenSpawn.Spawn(visual, cell, map);
            return visual;
        }

        public void Pulse()
        {
            lastPulseTick = ageTicks;
        }

        protected override void Tick()
        {
            base.Tick();
            ageTicks++;

            if (kind == DeepcallerVisualKind.Grasp
                && (target == null || target.Destroyed || !target.Spawned
                    || target is Pawn held
                    && !held.health.hediffSet.HasHediff(
                        Deepcaller_DefOf.Deepcaller_Grasped)))
            {
                Destroy(DestroyMode.Vanish);
                return;
            }

            if (ageTicks >= lifespanTicks)
                Destroy(DestroyMode.Vanish);
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            switch (kind)
            {
                case DeepcallerVisualKind.Riptide:
                    DeepcallerVisualRenderer.DrawRiptide(
                        drawLoc, ageTicks, lifespanTicks, radius, seed);
                    break;
                case DeepcallerVisualKind.Grasp:
                    DeepcallerVisualRenderer.DrawGrasp(
                        drawLoc, target, ageTicks, lastPulseTick, seed);
                    break;
                case DeepcallerVisualKind.GraspRelease:
                    DeepcallerVisualRenderer.DrawGraspRelease(
                        drawLoc, ageTicks, lifespanTicks, seed);
                    break;
                case DeepcallerVisualKind.Ink:
                    DeepcallerVisualRenderer.DrawInk(
                        drawLoc, ageTicks, lifespanTicks, radius, seed);
                    break;
                case DeepcallerVisualKind.ShieldBreak:
                    DeepcallerVisualRenderer.DrawShieldBreak(
                        drawLoc, ageTicks, lifespanTicks, radius, seed);
                    break;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref kind, "deepcallerVisualKind");
            Scribe_Values.Look(ref ageTicks, "deepcallerVisualAge");
            Scribe_Values.Look(ref lifespanTicks, "deepcallerVisualLifespan", 60);
            Scribe_Values.Look(ref radius, "deepcallerVisualRadius", 1f);
            Scribe_References.Look(ref target, "deepcallerVisualTarget");
            Scribe_Values.Look(ref seed, "deepcallerVisualSeed");
            Scribe_Values.Look(ref lastPulseTick, "deepcallerVisualPulse", -9999);
        }
    }

    /// <summary>
    /// Main-thread material cache and all realtime drawing for ability VFX.
    /// Keeping this in one place makes scale, cadence and visual altitude
    /// consistent across the whole path.
    /// </summary>
    public static class DeepcallerVisualRenderer
    {
        private const string Root = "Things/Deepcaller_Effects/";
        private static readonly int ColorProperty = Shader.PropertyToID("_Color");
        private static readonly MaterialPropertyBlock Properties = new MaterialPropertyBlock();

        private static Material[] riptide;
        private static Material[] grasp;
        private static Material graspTether;
        private static Material graspRelease;
        private static Material[] inkCore;
        private static Material[] inkPuff;
        private static Material[] tideguard;

        private static Material Mat(string path) =>
            MaterialPool.MatFrom(path, ShaderDatabase.TransparentPostLight, Color.white);

        private static void EnsureMaterials()
        {
            if (riptide != null)
                return;

            riptide = Frames(Root + "Riptide/Riptide_", 4);
            grasp = Frames(Root + "Grasp/Grasp_", 4);
            graspTether = Mat(Root + "Grasp/GraspTether");
            graspRelease = Mat(Root + "Grasp/GraspRelease");
            inkCore = Frames(Root + "Ink/InkCore_", 4);
            inkPuff = Frames(Root + "Ink/InkPuff_", 2);
            tideguard = Frames(Root + "Tideguard/Tideguard_", 4);
        }

        private static Material[] Frames(string prefix, int count)
        {
            var result = new Material[count];
            for (var i = 0; i < count; i++)
                result[i] = Mat(prefix + i);
            return result;
        }

        private static void Draw(
            Material material, Vector3 position, float angle, Vector3 scale,
            float alpha = 1f)
        {
            if (material == null || alpha <= 0.001f)
                return;
            Properties.Clear();
            Properties.SetColor(ColorProperty, new Color(1f, 1f, 1f, Mathf.Clamp01(alpha)));
            Graphics.DrawMesh(
                MeshPool.plane10,
                Matrix4x4.TRS(position, Quaternion.AngleAxis(angle, Vector3.up), scale),
                material, 0, null, 0, Properties);
        }

        private static float Fade(int age, int lifespan, int fadeIn = 10, int fadeOut = 18)
        {
            var into = Mathf.Clamp01(age / (float)Mathf.Max(fadeIn, 1));
            var outOf = Mathf.Clamp01((lifespan - age) / (float)Mathf.Max(fadeOut, 1));
            return Mathf.Min(into, outOf);
        }

        private static Vector3 AtAltitude(Vector3 position, AltitudeLayer altitude, float add = 0f)
        {
            position.y = altitude.AltitudeFor() + add;
            return position;
        }

        public static void DrawRiptide(
            Vector3 center, int age, int lifespan, float radius, int seed)
        {
            EnsureMaterials();
            var finalTicks = 18;
            int frame;
            if (age < 12)
                frame = 0;
            else if (age < 30)
                frame = 1;
            else if (age >= lifespan - finalTicks)
                frame = 3;
            else
                frame = 2;

            var intro = Mathf.SmoothStep(0.35f, 1f, Mathf.Clamp01(age / 24f));
            var size = Mathf.Max(2.4f, radius * 2f) * intro;
            var alpha = Fade(age, lifespan, 8, finalTicks);
            var angle = (seed % 360) + age * 2.2f;
            Draw(riptide[frame], AtAltitude(center, AltitudeLayer.MoteLow),
                angle, new Vector3(size, 1f, size), alpha);
        }

        public static void DrawGrasp(
            Vector3 anchor, Thing target, int age, int lastPulse, int seed)
        {
            EnsureMaterials();
            var frame = age < 10 ? 0 : 1 + ((age - 10) / 8 % 3);
            var pulse = Mathf.Clamp01(1f - (age - lastPulse) / 12f);
            var baseSize = 1.35f + pulse * 0.22f;
            var angle = Mathf.Sin((age + seed % 23) * 0.075f) * 5f;
            Draw(grasp[frame], AtAltitude(anchor, AltitudeLayer.MoteOverhead),
                angle, new Vector3(baseSize, 1f, baseSize), Mathf.Clamp01(age / 8f));

            if (target == null || !target.Spawned)
                return;
            var destination = target.DrawPos;
            destination.y = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.006f;
            var origin = AtAltitude(anchor, AltitudeLayer.MoteOverhead, 0.006f);
            var delta = destination - origin;
            delta.y = 0f;
            var length = delta.magnitude;
            if (length < 0.18f)
                return;

            var midpoint = (origin + destination) * 0.5f;
            var tetherAngle = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
            var width = 0.19f + pulse * 0.08f;
            Draw(graspTether, midpoint, tetherAngle,
                new Vector3(width, 1f, length), 0.72f + pulse * 0.28f);
        }

        public static void DrawGraspRelease(
            Vector3 center, int age, int lifespan, int seed)
        {
            EnsureMaterials();
            var progress = Mathf.Clamp01(age / (float)lifespan);
            var size = Mathf.Lerp(0.65f, 1.8f, progress);
            Draw(graspRelease, AtAltitude(center, AltitudeLayer.MoteOverhead),
                seed % 360, new Vector3(size, 1f, size), 1f - progress);
        }

        public static void DrawInk(
            Vector3 center, int age, int lifespan, float radius, int seed)
        {
            EnsureMaterials();
            var fade = Fade(age, lifespan, 20, 90);
            var coreFrame = age / 10 % 4;
            var breathe = 1f + 0.07f * Mathf.Sin((age + seed % 17) * 0.12f);
            var centerScale = 2.2f * breathe;
            Draw(inkCore[coreFrame],
                AtAltitude(center, AltitudeLayer.MoteOverhead, 0.016f),
                Mathf.Sin(age * 0.035f) * 4f,
                new Vector3(centerScale, 1f, centerScale), fade);

            var count = Mathf.Clamp(Mathf.RoundToInt(radius * 2f), 8, 28);
            for (var i = 0; i < count; i++)
            {
                var a = Hash01(seed + i * 7919);
                var d = Hash01(seed ^ (i * 104729 + 31));
                var s = Hash01(seed + i * 1543 + 97);
                var angle = a * Mathf.PI * 2f + age * (i % 2 == 0 ? 0.0008f : -0.00065f);
                var distance = Mathf.Sqrt(d) * radius * 0.88f;
                var drift = Mathf.Sin(age * 0.025f + i * 1.7f) * 0.16f;
                var offset = new Vector3(
                    Mathf.Sin(angle) * (distance + drift),
                    0f,
                    Mathf.Cos(angle) * (distance + drift));
                var puffScale = Mathf.Lerp(1.35f, 2.45f, s)
                    * (1f + 0.08f * Mathf.Sin(age * 0.055f + i));
                Draw(inkPuff[i & 1],
                    AtAltitude(center + offset, AltitudeLayer.MoteOverhead, 0.01f),
                    (a * 360f) + Mathf.Sin(age * 0.02f + i) * 7f,
                    new Vector3(puffScale, 1f, puffScale),
                    fade * Mathf.Lerp(0.52f, 0.82f, s));
            }
        }

        public static void DrawTideguard(
            Pawn pawn, float severity, float energyPercent, int ticksSinceImpact)
        {
            if (pawn == null || !pawn.Spawned)
                return;
            EnsureMaterials();
            var impact = Mathf.Clamp01(1f - ticksSinceImpact / 14f);
            var frame = (Find.TickManager.TicksGame / 7 + pawn.thingIDNumber) % 4;
            var stage = Mathf.Clamp(severity, 1f, 4f);
            var size = 1.32f + (stage - 1f) * 0.12f + impact * 0.14f;
            var alpha = Mathf.Lerp(0.32f, 0.72f, Mathf.Clamp01(energyPercent))
                + impact * 0.28f;
            var position = AtAltitude(pawn.DrawPos, AltitudeLayer.MoteOverhead, 0.02f);
            Draw(tideguard[frame], position,
                Find.TickManager.TicksGame * 0.18f + pawn.thingIDNumber % 360,
                new Vector3(size, 1f, size), Mathf.Clamp01(alpha));
        }

        public static void DrawShieldBreak(
            Vector3 center, int age, int lifespan, float scale, int seed)
        {
            EnsureMaterials();
            var progress = Mathf.Clamp01(age / (float)lifespan);
            var size = Mathf.Max(1.1f, scale) * Mathf.Lerp(1f, 1.45f, progress);
            Draw(tideguard[3], AtAltitude(center, AltitudeLayer.MoteOverhead, 0.025f),
                seed % 360 + age * 2.4f,
                new Vector3(size, 1f, size), 1f - progress);
        }

        private static float Hash01(int value)
        {
            unchecked
            {
                uint x = (uint)value;
                x ^= x >> 16;
                x *= 0x7feb352d;
                x ^= x >> 15;
                x *= 0x846ca68b;
                x ^= x >> 16;
                return (x & 0x00ffffff) / 16777215f;
            }
        }
    }
}
