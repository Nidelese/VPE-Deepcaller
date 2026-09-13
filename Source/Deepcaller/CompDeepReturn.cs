using RimWorld;
using Verse;

namespace Deepcaller
{
    /// VEF's CompDieAfterPeriod with a deep-flavored countdown — its stock
    /// inspect line reads "Animal expires in", which is no way to speak of
    /// a limb of a god. Swapped in via compClass on the race defs.
    public class CompDeepReturn : VEF.AnimalBehaviours.CompDieAfterPeriod
    {
        private int lifetimeGranted;
        public bool CanBenefitFromMeal => lifetimeGranted < Props.timeToDieInTicks
            && tickCounter > Props.timeToDieInTicks / 2;
        public void FeedDuration(int ticks)
        {
            int granted = CultivationMath.LifetimeGain(ticks, tickCounter, lifetimeGranted, Props.timeToDieInTicks);
            if (granted <= 0) return;
            tickCounter -= granted;
            lifetimeGranted += granted;
        }
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref lifetimeGranted, "lifetimeGranted");
        }
        public override string CompInspectStringExtra() =>
            "Deepcaller_ReturnsToDeep".Translate(
                (Props.timeToDieInTicks - tickCounter).ToStringTicksToPeriod());
    }
}
