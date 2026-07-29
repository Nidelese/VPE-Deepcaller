using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

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
        private int lastLocalPulseTick = -9999;
        private int lastWholeBodyPulseTick = -9999;
        private int lastThreatScanTick = -9999;
        private Thing sharedThreat;

        private const float ThreatAcquireRadius = 24.9f;
        private const float ThreatKeepRadius = 30f;
        private const float CommandFrontRadius = 8f;
        private const float PredatoryFormationLead = 0.9f;

        public Pawn Pawn => (Pawn)parent;
        public IReadOnlyList<Pawn> Parts => parts;
        public Thing SharedThreat =>
            ValidThreat(sharedThreat, ThreatKeepRadius, requireReach: false)
                ? sharedThreat
                : null;

        public void InitializePool(float amount) => poolMax = pool = amount;

        public void RegisterPart(Pawn part) => parts.Add(part);

        public override void CompTick()
        {
            base.CompTick();
            if (Pawn.Spawned && parent.IsHashIntervalTick(15))
                UpdateSharedThreat();
        }

        public void UpdateSharedThreat()
        {
            if (!Pawn.Spawned)
            {
                sharedThreat = null;
                return;
            }

            var tick = Find.TickManager.TicksGame;
            if (tick == lastThreatScanTick)
                return;
            lastThreatScanTick = tick;

            if (ValidThreat(
                    sharedThreat, ThreatKeepRadius, requireReach: true))
                return;

            sharedThreat = null;
            var bestScore = float.MaxValue;
            foreach (var candidate in
                     Pawn.Map.attackTargetsCache.GetPotentialTargetsFor(Pawn))
            {
                var target = candidate.Thing;
                if (!ValidThreat(
                        target, ThreatAcquireRadius, requireReach: true)
                    || candidate.ThreatDisabled(Pawn))
                    continue;

                var score =
                    target.Position.DistanceToSquared(Pawn.Position);
                if (score < bestScore)
                {
                    sharedThreat = target;
                    bestScore = score;
                }
            }
        }

        private bool ValidThreat(
            Thing target,
            float radius,
            bool requireReach)
        {
            if (target == null || target.Destroyed || !target.Spawned
                || target.Map != Pawn.Map || !target.HostileTo(Pawn)
                || !target.Position.InHorDistOf(Pawn.Position, radius)
                || target is Pawn targetPawn && targetPawn.Downed)
                return false;

            return !requireReach || Pawn.CanReach(
                target, PathEndMode.Touch, Danger.Deadly);
        }

        public bool IsInCommandFront(Thing target)
        {
            var command = SharedThreat;
            return command != null && target != null && target.Spawned
                   && target.Position.InHorDistOf(
                       command.Position, CommandFrontRadius);
        }

        /// <summary>
        /// At rest every aperture uses a pure radial slot. Once the head
        /// chooses a threat, the entire ring shifts one cell toward it before
        /// the head advances. Repeating that gather-then-advance cadence makes
        /// the many pawns read as one crawling body.
        /// </summary>
        public Vector3 FormationOffset(float angle, float radius)
        {
            var radial =
                Quaternion.AngleAxis(angle, Vector3.up)
                * Vector3.forward * radius;
            var threat = SharedThreat;
            if (threat == null)
                return radial;

            var delta = threat.Position - Pawn.Position;
            if (delta == IntVec3.Zero)
                return radial;

            var heading =
                new Vector3(delta.x, 0f, delta.z).normalized;
            return radial + heading * PredatoryFormationLead;
        }

        public void TakeSharedDamage(float amount, Pawn struckPart)
        {
            if (collapsing || amount <= 0f)
                return;
            pool -= amount;
            SignalSharedWound(struckPart);
            if (pool <= 0f)
                Collapse();
        }

        /// <summary>
        /// The head advances only when the submerged body has brought its
        /// apertures back into a plausible shape. Predating arms may extend,
        /// but their extension makes the central body brace instead of
        /// abandoning them.
        /// </summary>
        public bool FormationReadyToAdvance()
        {
            foreach (var part in parts)
            {
                if (part == null || part == Pawn || part.Dead || !part.Spawned)
                    continue;

                var anatomy = part.TryGetComp<CompLeviathanPart>();
                var anchor = anatomy?.AnchorCell ?? IntVec3.Invalid;
                if (!anchor.IsValid)
                    continue;

                var tolerance = anatomy.IsFlower ? 0.55f : 0.8f;
                if (!part.Position.InHorDistOf(anchor, tolerance))
                    return false;
            }
            return true;
        }

        public int PredationClaims(Thing target, Pawn except)
        {
            var claims = 0;
            foreach (var part in parts)
            {
                if (part == null || part == except || part.Dead)
                    continue;
                if (part.mindState?.enemyTarget == target)
                    claims++;
            }
            return claims;
        }

        /// <summary>
        /// There is no visible surface umbilical. Damage instead contracts the
        /// rift at the wounded organ and the head, with an occasional
        /// whole-body sympathetic pulse proving all apertures share one body.
        /// </summary>
        private void SignalSharedWound(Pawn struckPart)
        {
            if (!Pawn.Spawned)
                return;

            var tick = Find.TickManager.TicksGame;
            if (tick - lastLocalPulseTick >= 12)
            {
                PulsePortal(struckPart);
                if (struckPart != Pawn)
                    PulsePortal(Pawn);
                lastLocalPulseTick = tick;
            }

            if (tick - lastWholeBodyPulseTick < 60)
                return;

            foreach (var part in parts)
                PulsePortal(part, subtle: true);
            lastWholeBodyPulseTick = tick;
        }

        private static void PulsePortal(Pawn part, bool subtle = false)
        {
            if (part == null || part.Dead || !part.Spawned)
                return;

            var anatomy = part.TryGetComp<CompLeviathanPart>();
            var scale = anatomy?.IsCore == true
                ? 0.7f
                : anatomy?.IsFlower == true ? 0.42f : 0.34f;
            if (subtle)
                scale *= 0.72f;

            // Skip-rift flecks leave a crescent at the old cell after a limb
            // moves, recreating the infamous floating "portal hat". A small
            // tinted ground puff communicates a shared contraction without
            // leaving any directional graphic behind.
            FleckMaker.ThrowDustPuffThick(
                part.DrawPos,
                part.Map,
                scale,
                DeepPullUtility.DeepColor);
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
            Scribe_References.Look(ref sharedThreat, "sharedThreat");
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
        // An arm begins at its formation radius and may reach this much
        // farther before prey leaves the submerged body's anatomical envelope.
        public float predationExtension = 2.25f;
        // Full angular sector centered on the arm's assigned hardpoint.
        public float predationArcDegrees = 150f;
        // Beyond the legal extension, allow a brief grace distance before the
        // aperture closes and reopens at its hardpoint.
        public float resurfaceGraceDistance = 1.5f;

        public CompProperties_LeviathanPart() => compClass = typeof(CompLeviathanPart);
    }

    /// Present on every tentacle race; inert until an ability links it to
    /// a core. Linked parts have no health of their own — all damage is
    /// absorbed into the core's pool. Each limb remembers a formation
    /// hardpoint. The connective anatomy remains below the rift membrane;
    /// shared wound pulses and coordinated motion communicate that unity.
    public class CompLeviathanPart : ThingComp
    {
        public Pawn core;
        public string role = "arm";
        public int slotIndex = -1;
        public int slotCount = 1;
        public float anchorAngle;
        public float anchorRadius;

        public CompProperties_LeviathanPart Props =>
            (CompProperties_LeviathanPart)props;

        public CompLeviathanCore Core =>
            core != null && !core.Dead ? core.TryGetComp<CompLeviathanCore>() : null;

        public bool IsLinked => Core != null;
        public bool IsCore => core == parent;
        public bool IsFlower => role == "flower";
        public float MaximumPredationRadius =>
            anchorRadius + Mathf.Max(0f, Props.predationExtension);

        public void Initialize(
            Pawn newCore,
            string newRole,
            int newSlotIndex,
            int newSlotCount,
            float newAnchorAngle,
            float newAnchorRadius)
        {
            core = newCore;
            role = newRole ?? "arm";
            slotIndex = newSlotIndex;
            slotCount = Mathf.Max(1, newSlotCount);
            anchorAngle = newAnchorAngle;
            anchorRadius = Mathf.Max(0f, newAnchorRadius);
        }

        public IntVec3 AnchorCell
        {
            get
            {
                var pawn = parent as Pawn;
                if (core == null || pawn?.Map == null || core.Map != pawn.Map)
                    return IntVec3.Invalid;

                var radial = Core?.FormationOffset(
                                 anchorAngle, anchorRadius)
                             ?? Quaternion.AngleAxis(
                                 anchorAngle, Vector3.up)
                             * Vector3.forward * anchorRadius;
                var preferred = core.Position + radial.ToIntVec3();
                preferred = preferred.ClampInsideMap(pawn.Map);

                var best = IntVec3.Invalid;
                var bestScore = float.MaxValue;
                foreach (var cell in GenRadial.RadialCellsAround(
                             preferred, 2.2f, true))
                {
                    if (!cell.InBounds(pawn.Map) || !cell.Standable(pawn.Map))
                        continue;

                    var score = cell.DistanceToSquared(preferred) * 5f
                                + Mathf.Abs(
                                    cell.DistanceTo(core.Position)
                                    - anchorRadius) * 2f;

                    var coreComp = Core;
                    if (coreComp != null)
                    {
                        foreach (var sibling in coreComp.Parts)
                        {
                            if (sibling == null || sibling == pawn
                                || sibling.Dead || !sibling.Spawned)
                                continue;
                            var separation = cell.DistanceToSquared(
                                sibling.Position);
                            if (separation == 0)
                                score += 1000f;
                            else if (separation <= 2)
                                score += 5f;
                        }
                    }

                    if (score < bestScore)
                    {
                        best = cell;
                        bestScore = score;
                    }
                }
                return best;
            }
        }

        public bool CanPredate(Thing target)
        {
            if (!IsLinked || IsCore || IsFlower || target == null
                || target.Destroyed || !target.Spawned
                || target.Map != core.Map)
                return false;

            var fromCore = target.Position - core.Position;
            var distance = fromCore.LengthHorizontal;
            if (distance > MaximumPredationRadius)
                return false;

            // Anything beneath the central mantle can be reached by all arms.
            if (distance <= 1.5f)
                return true;

            var sectorDirection =
                Quaternion.AngleAxis(anchorAngle, Vector3.up) * Vector3.forward;
            var targetDirection =
                new Vector3(fromCore.x, 0f, fromCore.z).normalized;
            var minimumDot = Mathf.Cos(
                Mathf.Clamp(Props.predationArcDegrees, 1f, 360f)
                * 0.5f * Mathf.Deg2Rad);
            return Vector3.Dot(sectorDirection, targetDirection) >= minimumDot;
        }

        public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            absorbed = false;
            var coreComp = Core;
            if (coreComp == null)
                return;
            coreComp.TakeSharedDamage(dinfo.Amount, (Pawn)parent);
            absorbed = true;
        }

        // A linked arm never receives enough time to turn a short extension
        // into a cross-map chase. This is the hard anatomical safeguard under
        // the formation-aware target selection and short combat jobs.
        public override void CompTick()
        {
            base.CompTick();
            if (core == null || !parent.IsHashIntervalTick(15))
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

            var target = pawn.mindState?.enemyTarget;
            if (target != null && !CanPredate(target))
            {
                pawn.mindState.enemyTarget = null;
                if (pawn.CurJobDef == JobDefOf.AttackMelee)
                    pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
            }

            var hardLimit =
                MaximumPredationRadius + Props.resurfaceGraceDistance;
            if (!pawn.Position.InHorDistOf(core.Position, hardLimit))
            {
                pawn.mindState.enemyTarget = null;
                TryResurfaceAtAnchor();
            }
        }

        public bool TryResurfaceAtAnchor()
        {
            if (!(parent is Pawn pawn) || !pawn.Spawned)
                return false;

            var map = pawn.Map;
            var anchor = AnchorCell;
            var cell = anchor.IsValid
                ? CellFinder.StandableCellNear(anchor, map, 1.9f)
                : CellFinder.StandableCellNear(core.Position, map, 2f);
            if (!cell.IsValid || cell == pawn.Position)
                return false;

            FleckMaker.ThrowDustPuffThick(
                pawn.DrawPos,
                map,
                IsFlower ? 0.75f : 1f,
                DeepPullUtility.DeepColor);
            pawn.Position = cell;
            pawn.Notify_Teleported(endCurrentJob: true, resetTweenedPos: true);
            FleckMaker.ThrowDustPuffThick(
                pawn.DrawPos,
                map,
                IsFlower ? 0.75f : 1f,
                DeepPullUtility.DeepColor);
            return true;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look(ref core, "core");
            Scribe_Values.Look(ref role, "role", "arm");
            Scribe_Values.Look(ref slotIndex, "slotIndex", -1);
            Scribe_Values.Look(ref slotCount, "slotCount", 1);
            Scribe_Values.Look(ref anchorAngle, "anchorAngle");
            Scribe_Values.Look(ref anchorRadius, "anchorRadius");
        }
    }
}
