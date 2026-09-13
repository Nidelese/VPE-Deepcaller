using System.Collections.Generic;

namespace Deepcaller
{
    // Several pre-1.6 mods give stone/metal a custom FleshTypeDef without
    // setting isOrganic=false. Keep those known materials out of the diet.
    public static class PhysicalTargetRules
    {
        private static readonly HashSet<string> ConstructFlesh = new HashSet<string>
        {
            "VPE_SteelConstructFlesh", "VPE_RockConstructFlesh", "TM_StoneFlesh", "TM_MechaGolemFlesh"
        };
        private static readonly HashSet<string> ConstructRaces = new HashSet<string>
        {
            "TM_HollowGolem", "TM_MaharalGolem", "TM_StrawGolem", "TM_WoodGolem", "TM_SilkGolem"
        };

        public static bool NonOrganic(bool organicFlesh, bool mechanicalCorpse, string flesh, string race) =>
            !organicFlesh || mechanicalCorpse || ConstructFlesh.Contains(flesh ?? "") || ConstructRaces.Contains(race ?? "");

        public static bool BudCanAttack(bool nonOrganic, int unlockRank) => !nonOrganic || unlockRank > 0;
    }
}
