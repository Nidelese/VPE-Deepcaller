using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Deepcaller
{
    /// Raise Idol, but re-castable and nomad-proof: if an idol already
    /// stands on ANY map, the cast MOVES the god instead of duplicating it;
    /// if the god withdrew when its map was abandoned (Building_DeepIdol),
    /// the cast restores it. VEF's base Cast is one big housekeeping method
    /// we can't partially skip, so we let it spawn the new idol normally,
    /// transplant state into it, and vanish the old shell.
    public class Ability_RaiseIdol : VEF.Abilities.Ability_SpawnBuilding
    {
        public override void Cast(params GlobalTargetInfo[] targets)
        {
            var old = BestIdolAnywhere(pawn.Faction);
            base.Cast(targets);

            foreach (var target in targets)
            {
                var fresh = target.Cell.GetFirstBuilding(pawn.Map)?.TryGetComp<CompIdolDevotion>();
                if (fresh == null)
                    continue;

                if (old != null && old != fresh && !old.parent.Destroyed)
                {
                    fresh.TransplantFrom(old);
                    if (old.parent.Spawned)
                        for (var i = 0; i < 6; i++)
                            FleckMaker.ThrowDustPuffThick(old.parent.DrawPos, old.parent.Map, 2f, DeepPullUtility.DeepColor);
                    old.parent.Destroy();
                    Messages.Message("Deepcaller_IdolMoved".Translate(),
                        fresh.parent, MessageTypeDefOf.PositiveEvent);
                }
                else if (DeepcallerGameComponent.Instance is { HasWithdrawnGod: true } store)
                {
                    fresh.RestoreFrom(store);
                    Messages.Message("Deepcaller_IdolReturned".Translate(),
                        fresh.parent, MessageTypeDefOf.PositiveEvent);
                }
                break;
            }
        }

        private static CompIdolDevotion BestIdolAnywhere(Faction faction)
        {
            CompIdolDevotion best = null;
            foreach (var map in Find.Maps)
            {
                var comp = CompIdolDevotion.BestIdol(map, faction);
                if (comp != null && (best == null || comp.Devotion > best.Devotion))
                    best = comp;
            }
            return best;
        }
    }
}
