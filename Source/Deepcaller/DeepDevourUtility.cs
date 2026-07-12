using RimWorld;
using Verse;

namespace Deepcaller
{
    /// Shared "the deep eats someone" ending: instant death, no corpse,
    /// the flesh offered to the faction's best idol. Callers handle gear
    /// beforehand (hoard-stash, strip, or let it digest).
    public static class DeepDevourUtility
    {
        public static void Devour(Pawn victim, Faction beneficiary, float bodySizeEquivalent)
        {
            var map = victim.MapHeld;
            var position = victim.PositionHeld;
            if (map != null)
            {
                FilthMaker.TryMakeFilth(position, map, ThingDefOf.Filth_Blood, 3);
                for (var i = 0; i < 6; i++)
                    FleckMaker.ThrowDustPuffThick(victim.DrawPos, map, 2f, DeepPullUtility.DeepColor);
            }
            victim.Kill(null);
            victim.Corpse?.Destroy();
            CompIdolDevotion.OfferRemote(map, beneficiary, bodySizeEquivalent);
        }
    }
}
