using RimWorld;
using RimWorld.Planet;
using VEF.Abilities;
using Verse;

namespace Deepcaller
{
    public class AbilityExtension_SummonPawn : DefModExtension
    {
        public PawnKindDef kind;
        public int count = 1;
    }

    public class Ability_SummonTentacle : VEF.Abilities.Ability
    {
        public override void Cast(params GlobalTargetInfo[] targets)
        {
            base.Cast(targets);
            var ext = def.GetModExtension<AbilityExtension_SummonPawn>();
            if (ext?.kind == null)
            {
                Log.Error($"[Deepcaller] {def.defName} has no AbilityExtension_SummonPawn with a pawn kind.");
                return;
            }

            foreach (var target in targets)
            {
                for (var i = 0; i < ext.count; i++)
                {
                    // Fixed age 0: growth stages are life stages, so summons
                    // must be born as Sprouts, not at a random adult age.
                    var request = new PawnGenerationRequest(ext.kind, pawn.Faction,
                        PawnGenerationContext.NonPlayer, -1,
                        forceGenerateNewPawn: true,
                        fixedBiologicalAge: 0f, fixedChronologicalAge: 0f);
                    var summon = PawnGenerator.GeneratePawn(request);
                    var cell = CellFinder.StandableCellNear(target.Cell, pawn.Map, 2f);
                    GenSpawn.Spawn(summon, cell, pawn.Map);
                    summon.TryGetComp<CompTentacleGrowth>()
                        ?.GrantStage(CompIdolDevotion.HighestDevotionLevel(pawn.Map, pawn.Faction));
                    TideguardUtility.TryApply(summon, pawn);
                }
            }
        }
    }
}
