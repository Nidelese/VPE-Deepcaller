using RimWorld;
using Verse;

namespace Deepcaller
{
    /// VEF's CompDieAfterPeriod with a deep-flavored countdown — its stock
    /// inspect line reads "Animal expires in", which is no way to speak of
    /// a limb of a god. Swapped in via compClass on the race defs.
    public class CompDeepReturn : VEF.AnimalBehaviours.CompDieAfterPeriod
    {
        public override string CompInspectStringExtra() =>
            "Deepcaller_ReturnsToDeep".Translate(
                (Props.timeToDieInTicks - tickCounter).ToStringTicksToPeriod());
    }
}
