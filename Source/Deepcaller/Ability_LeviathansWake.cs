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
        public int armCount = 8;
        public float ringRadius = 3f;
        public int armStage = 2; // arms are born Crushers

        // Shared pool: base + perDevotion x idol devotion at cast.
        public float basePool = 1500f;
        public float poolPerDevotion = 2f;
    }

    /// The capstone: wake THE LEVIATHAN. A head that spits poison sludge,
    /// ringed by eight arms in fixed positions, all sharing one single
    /// giant health pool (CompLeviathanCore).
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
                core?.InitializePool(ext.basePool + ext.poolPerDevotion * devotion);
                core?.RegisterPart(head);
                if (core != null)
                    core.summoner = pawn;
                Link(head, head);

                for (var i = 0; i < ext.armCount; i++)
                {
                    var angle = 360f / ext.armCount * i;
                    var offset = (Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward * ext.ringRadius).ToIntVec3();
                    var cell = CellFinder.StandableCellNear(center + offset, map, 2f);
                    if (!cell.IsValid)
                        continue;
                    var arm = SpawnPart(ext.armKind, cell, map);
                    arm.TryGetComp<CompTentacleGrowth>()?.GrantStage(ext.armStage);
                    // Arms may be shielded; the head never is (its race has
                    // no shield comp, and we don't apply the hediff).
                    TideguardUtility.TryApply(arm, pawn);
                    core?.RegisterPart(arm);
                    Link(arm, head);
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

        private static void Link(Pawn part, Pawn head)
        {
            var comp = part.TryGetComp<CompLeviathanPart>();
            if (comp != null)
                comp.core = head;
        }
    }
}
