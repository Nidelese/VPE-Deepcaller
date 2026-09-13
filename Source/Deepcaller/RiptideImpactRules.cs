namespace Deepcaller
{
    public enum RiptideImpact { None, Stop, Mine, Smash }

    public static class RiptideImpactRules
    {
        public static bool FavorActive(int purchasedRank, bool localIdolSated) =>
            purchasedRank > 0 && localIdolSated;

        public static bool AffectsPawn(bool hostile, int hostilesOnlyRank, bool localIdolSated) =>
            hostile || !FavorActive(hostilesOnlyRank, localIdolSated);

        public static bool AffectsPossession(bool playerOwned, int possessionsRank, bool localIdolSated) =>
            !playerOwned || !FavorActive(possessionsRank, localIdolSated);

        // Pickup favors never confer collision immunity. The wall favor is
        // the only promise to prevent damage, and applies only to barriers.
        public static bool CollisionVulnerable(bool playerBarrier, int wallsRank, bool localIdolSated) =>
            !playerBarrier || !FavorActive(wallsRank, localIdolSated);

        // No body means no impact. Natural rock is always a mining collision;
        // sparing our constructed obstacles requires both favor and payment.
        public static RiptideImpact Resolve(bool bodyAtObstacle, bool naturalRock,
            bool playerStructure, int purchasedRank, bool localIdolSated)
        {
            if (!bodyAtObstacle) return RiptideImpact.None;
            if (naturalRock) return RiptideImpact.Mine;
            return CollisionVulnerable(playerStructure, purchasedRank, localIdolSated)
                ? RiptideImpact.Smash : RiptideImpact.Stop;
        }
    }
}
