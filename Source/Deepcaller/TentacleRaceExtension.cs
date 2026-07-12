using Verse;

namespace Deepcaller
{
    public class TentacleRaceExtension : DefModExtension
    {
        /// Multiplies AttackTargetFinder's score for this race: enemies treat
        /// tentacles as priority targets instead of routing around them.
        public float targetPriorityFactor = 2.5f;
    }
}
