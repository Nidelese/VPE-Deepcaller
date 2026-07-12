using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Deepcaller
{
    public class CompProperties_LeviathanCore : CompProperties
    {
        public CompProperties_LeviathanCore() => compClass = typeof(CompLeviathanCore);
    }

    /// The Leviathan's single shared health pool, living on the head. All
    /// parts (arms and the head itself) forward their damage here; when it
    /// empties, the whole god dies at once. Focus-firing one arm is
    /// pointless by construction.
    public class CompLeviathanCore : ThingComp
    {
        // The Deepcaller who woke it; the head follows them between fights.
        public Pawn summoner;

        private float poolMax;
        private float pool;
        private List<Pawn> parts = new List<Pawn>();
        private bool collapsing;

        public Pawn Pawn => (Pawn)parent;

        public void InitializePool(float amount) => poolMax = pool = amount;

        public void RegisterPart(Pawn part) => parts.Add(part);

        public void TakeSharedDamage(float amount)
        {
            if (collapsing || amount <= 0f)
                return;
            pool -= amount;
            if (pool <= 0f)
                Collapse();
        }

        /// The pool is spent: every part dies together. Kill() is not
        /// damage, so nothing re-enters the absorb path.
        private void Collapse()
        {
            collapsing = true;
            for (var i = parts.Count - 1; i >= 0; i--)
            {
                var part = parts[i];
                if (part != null && !part.Dead && part != Pawn)
                    part.Kill(null);
            }
            if (!Pawn.Dead)
                Pawn.Kill(null);
        }

        public override string CompInspectStringExtra() =>
            "Deepcaller_LeviathanPool".Translate(Mathf.Max(pool, 0f).ToString("0"), poolMax.ToString("0"));

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look(ref summoner, "summoner");
            Scribe_Values.Look(ref poolMax, "poolMax");
            Scribe_Values.Look(ref pool, "pool");
            Scribe_Values.Look(ref collapsing, "collapsing");
            Scribe_Collections.Look(ref parts, "parts", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                parts ??= new List<Pawn>();
                parts.RemoveAll(p => p == null);
            }
        }
    }

    public class CompProperties_LeviathanPart : CompProperties
    {
        // Arms are tethered to the head: beyond this range they are
        // dragged back (the deep keeps its shape).
        public float tetherRadius = 4.5f;

        public CompProperties_LeviathanPart() => compClass = typeof(CompLeviathanPart);
    }

    /// Present on every tentacle race; inert until an ability links it to
    /// a core. Linked parts have no health of their own — all damage is
    /// absorbed into the core's pool — and stay tethered to the head.
    public class CompLeviathanPart : ThingComp
    {
        public Pawn core;

        public CompProperties_LeviathanPart Props => (CompProperties_LeviathanPart)props;

        private CompLeviathanCore Core =>
            core != null && !core.Dead ? core.TryGetComp<CompLeviathanCore>() : null;

        public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            absorbed = false;
            var coreComp = Core;
            if (coreComp == null)
                return;
            coreComp.TakeSharedDamage(dinfo.Amount);
            if (parent.Spawned)
                FleckMaker.ThrowDustPuffThick(parent.DrawPos, parent.Map, 1f, DeepPullUtility.DeepColor);
            absorbed = true;
        }

        // CompTick, not CompTickRare: the head is mobile now, and a 250-tick
        // tether check lets arms trail ~10 cells behind before snapping.
        public override void CompTick()
        {
            base.CompTick();
            if (core == null || !parent.IsHashIntervalTick(60))
                return;
            var pawn = (Pawn)parent;
            if (core.Dead || !core.Spawned)
            {
                // Orphaned limb: the god is gone, the flesh follows.
                if (!pawn.Dead)
                    pawn.Kill(null);
                return;
            }
            if (pawn == core || !pawn.Spawned)
                return;
            var dist = pawn.Position.DistanceTo(core.Position);
            if (dist > Props.tetherRadius * 3f)
            {
                // Hopelessly separated (the line pull stops dead at walls, and
                // walking may be too slow to ever catch up): withdraw into the
                // ground and re-erupt beside the head.
                Resurface(pawn);
            }
            else if (dist > Props.tetherRadius)
            {
                DeepPullUtility.PullTowards(pawn, core.Position, 4, Props.tetherRadius - 1f);
            }
        }

        private void Resurface(Pawn pawn)
        {
            var map = pawn.Map;
            var cell = CellFinder.StandableCellNear(core.Position, map, Props.tetherRadius - 1f);
            if (!cell.IsValid || cell == pawn.Position)
                return;
            FleckMaker.ThrowDustPuffThick(pawn.DrawPos, map, 2f, DeepPullUtility.DeepColor);
            pawn.Position = cell;
            pawn.Notify_Teleported(endCurrentJob: true, resetTweenedPos: true);
            FleckMaker.ThrowDustPuffThick(pawn.DrawPos, map, 2f, DeepPullUtility.DeepColor);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look(ref core, "core");
        }
    }
}
