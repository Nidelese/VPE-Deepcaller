using RimWorld;
using UnityEngine;
using Verse;

namespace Deepcaller
{
    /// Shared "the deep drags you" movement, used by the Grasp leash and
    /// Riptide's burst pull.
    public static class DeepPullUtility
    {
        public static readonly Color DeepColor = new Color(0.35f, 0.55f, 0.5f);

        /// Drags the victim up to maxCells toward anchor along the sight
        /// line, stopping at the first cell that can't be stood on (walls,
        /// closed doors) or once within stopWithin of the anchor. Interrupts
        /// the victim's current job. Returns true if it moved at all.
        public static bool PullTowards(Pawn victim, IntVec3 anchor, int maxCells, float stopWithin = 0f)
        {
            if (!victim.Spawned || victim.Position == anchor)
                return false;

            var map = victim.Map;
            var dest = victim.Position;
            var moved = 0;
            foreach (var cell in GenSight.PointsOnLineOfSight(victim.Position, anchor))
            {
                if (cell == victim.Position)
                    continue;
                if (!cell.Standable(map))
                    break;
                dest = cell;
                moved++;
                if (moved >= maxCells || (stopWithin > 0f && cell.InHorDistOf(anchor, stopWithin)))
                    break;
            }
            if (moved == 0)
                return false;

            FleckMaker.ThrowDustPuffThick(victim.DrawPos, map, 1.5f, DeepColor);
            victim.Position = dest;
            victim.Notify_Teleported(endCurrentJob: true, resetTweenedPos: true);
            FleckMaker.ThrowDustPuffThick(victim.DrawPos, map, 1.5f, DeepColor);
            return true;
        }
    }
}
