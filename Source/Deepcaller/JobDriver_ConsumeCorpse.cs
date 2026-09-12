using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Deepcaller
{
    public class JobDriver_ConsumeCorpse : JobDriver
    {
        private const int DevourTicks = 360;

        private Corpse Corpse => (Corpse)job.targetA.Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
            => pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            var devour = Toils_General.Wait(DevourTicks, TargetIndex.A);
            devour.WithProgressBarToilDelay(TargetIndex.A);
            devour.FailOnDespawnedOrNull(TargetIndex.A);
            devour.FailOn(() => CompIdolDevotion.ClaimsCorpse(Corpse, pawn.Faction)
                || pawn.TryGetComp<CompTentacleGrowth>()?.CanBenefitFromMeal != true);
            yield return devour;

            yield return Toils_General.Do(() =>
            {
                var corpse = Corpse;
                if (CompIdolDevotion.ClaimsCorpse(corpse, pawn.Faction)
                    || pawn.TryGetComp<CompTentacleGrowth>()?.CanBenefitFromMeal != true) return;
                var meal = corpse.InnerPawn.BodySize;
                FilthMaker.TryMakeFilth(corpse.Position, pawn.Map, ThingDefOf.Filth_Blood, 3);
                corpse.Destroy();

                var growth = pawn.TryGetComp<CompTentacleGrowth>();
                if (growth != null)
                {
                    growth.EatMeal(meal);
                }
            });
        }
    }
}
