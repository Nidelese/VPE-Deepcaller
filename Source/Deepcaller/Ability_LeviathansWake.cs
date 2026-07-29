using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Deepcaller
{
    public class AbilityExtension_Leviathan : DefModExtension
    {
        public PawnKindDef headKind;
        public PawnKindDef armKind;
        public PawnKindDef flowerKind;
        public int armCount = 8;
        public int flowerCount = 3;
        public float ringRadius = 3f;
        public float flowerRingRadius = 2.15f;

        // Shared pool: base + perDevotion x idol devotion at cast.
        public float basePool = 1500f;
        public float poolPerDevotion = 2f;
    }

    /// The capstone: wake THE LEVIATHAN. The head, eight independently
    /// thinking arms, and three independently targeting flowers are separate
    /// pawns linked into one living rig and one shared health pool.
    public class Ability_LeviathansWake : VEF.Abilities.Ability
    {
        private AbilityExtension_Leviathan Ext => def.GetModExtension<AbilityExtension_Leviathan>();

        public override bool IsEnabledForPawn(out string reason)
        {
            if (!CompIdolDevotion.AnyIdol(pawn.MapHeld, pawn.Faction))
            {
                reason = "Deepcaller_ConsumeNoIdol".Translate();
                return false;
            }
            return base.IsEnabledForPawn(out reason);
        }

        public override void Cast(params GlobalTargetInfo[] targets)
        {
            base.Cast(targets);
            var ext = Ext;
            var map = pawn.Map;
            var devotion = CompIdolDevotion.HighestDevotion(map, pawn.Faction);

            foreach (var target in targets)
            {
                var center = CellFinder.StandableCellNear(target.Cell, map, 2f);
                var head = SpawnPart(ext.headKind, center, map);
                var core = head.TryGetComp<CompLeviathanCore>();
                var occupied = new HashSet<IntVec3> { center };
                core?.InitializePool(ext.basePool + ext.poolPerDevotion * devotion);
                core?.RegisterPart(head);
                if (core != null)
                    core.summoner = pawn;
                Link(head, head, "core", 0, 1, 0f, 0f);

                for (var i = 0; i < ext.armCount; i++)
                {
                    var angle = 360f / ext.armCount * i;
                    var offset = (Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward * ext.ringRadius).ToIntVec3();
                    var cell = FindPartCell(center + offset, map, occupied);
                    if (!cell.IsValid)
                        continue;
                    var arm = SpawnPart(ext.armKind, cell, map);
                    // Arms may be shielded; the head never is (its race has
                    // no shield comp, and we don't apply the hediff).
                    TideguardUtility.TryApply(arm, pawn);
                    core?.RegisterPart(arm);
                    Link(arm, head, "arm", i, ext.armCount, angle, ext.ringRadius);
                    occupied.Add(cell);
                }

                for (var i = 0; i < ext.flowerCount && ext.flowerKind != null; i++)
                {
                    // Offset the inner crown from the cardinal arm slots so
                    // the two rings interlock instead of stacking visually.
                    var angle = 360f / ext.flowerCount * i + 60f;
                    var offset = (Quaternion.AngleAxis(angle, Vector3.up)
                                  * Vector3.forward * ext.flowerRingRadius).ToIntVec3();
                    var cell = FindPartCell(center + offset, map, occupied);
                    if (!cell.IsValid)
                        continue;
                    var flower = SpawnPart(ext.flowerKind, cell, map);
                    core?.RegisterPart(flower);
                    Link(flower, head, "flower", i, ext.flowerCount, angle,
                        ext.flowerRingRadius);
                    occupied.Add(cell);
                }

                for (var i = 0; i < 12; i++)
                    FleckMaker.ThrowDustPuffThick(center.ToVector3Shifted(), map, 3f, DeepPullUtility.DeepColor);
            }
        }

        private Pawn SpawnPart(PawnKindDef kind, IntVec3 cell, Map map)
        {
            var request = new PawnGenerationRequest(kind, pawn.Faction,
                PawnGenerationContext.NonPlayer, -1,
                forceGenerateNewPawn: true,
                fixedBiologicalAge: 0f, fixedChronologicalAge: 0f);
            var part = PawnGenerator.GeneratePawn(request);
            GenSpawn.Spawn(part, cell, map);
            return part;
        }

        private static IntVec3 FindPartCell(
            IntVec3 preferred,
            Map map,
            HashSet<IntVec3> occupied)
        {
            foreach (var cell in GenRadial.RadialCellsAround(preferred, 2f, true))
            {
                if (cell.InBounds(map) && cell.Standable(map)
                    && !occupied.Contains(cell))
                    return cell;
            }
            return IntVec3.Invalid;
        }

        private static void Link(
            Pawn part,
            Pawn head,
            string role,
            int slotIndex,
            int slotCount,
            float anchorAngle,
            float anchorRadius)
        {
            var comp = part.TryGetComp<CompLeviathanPart>();
            if (comp != null)
                comp.Initialize(head, role, slotIndex, slotCount, anchorAngle,
                    anchorRadius);
        }
    }
}
