using RimWorld;
using Verse;
using Verse.AI;

namespace Deepcaller
{
    public class JobGiver_ConsumeNearbyCorpse : ThinkNode_JobGiver
    {
        private const float SearchRadius = 15f;

        protected override Job TryGiveJob(Pawn pawn)
        {
            var growth = pawn.TryGetComp<CompTentacleGrowth>();
            if (growth == null || !growth.CanBenefitFromMeal)
                return null;
            if (!DeepcallerGameComponent.TentaclesEatCorpses)
                return null;

            var corpse = (Corpse)GenClosest.ClosestThingReachable(
                pawn.Position, pawn.Map,
                ThingRequest.ForGroup(ThingRequestGroup.Corpse),
                PathEndMode.Touch,
                TraverseParms.For(pawn),
                SearchRadius,
                t => t is Corpse c && IsValidMeal(pawn, c));

            if (corpse == null)
                return null;

            return JobMaker.MakeJob(Deepcaller_DefOf.Deepcaller_ConsumeCorpse, corpse);
        }

        private static bool IsValidMeal(Pawn pawn, Corpse corpse)
        {
            return corpse.InnerPawn.Faction != pawn.Faction
                && !corpse.InnerPawn.RaceProps.IsMechanoid
                && !corpse.InnerPawn.def.HasModExtension<TentacleRaceExtension>()
                && corpse.GetRotStage() != RotStage.Dessicated
                && !CompIdolDevotion.ClaimsCorpse(corpse, pawn.Faction)
                && pawn.CanReserve(corpse);
        }
    }
}
