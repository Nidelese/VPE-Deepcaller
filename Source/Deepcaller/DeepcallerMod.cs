using HarmonyLib;
using RimWorld;
using Verse;

namespace Deepcaller
{
    public class DeepcallerMod : Mod
    {
        public DeepcallerMod(ModContentPack content) : base(content)
        {
            new Harmony("Nidelese.VPE.Deepcaller").PatchAll();
        }
    }

    [DefOf]
    public static class Deepcaller_DefOf
    {
        public static JobDef Deepcaller_ConsumeCorpse;
        public static ThingDef Deepcaller_Idol;
        public static HediffDef Deepcaller_Riptiden;
        public static HediffDef Deepcaller_Inked;
        public static VEF.Abilities.AbilityDef Deepcaller_DeepHoard;
        public static TraderKindDef Deepcaller_DeepTrader;
        public static HediffDef Deepcaller_Symbiote;
        public static VEF.Abilities.AbilityDef Deepcaller_Tideguard;
        public static HediffDef Deepcaller_TideguardShield;

        static Deepcaller_DefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(Deepcaller_DefOf));
    }
}
