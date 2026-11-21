using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Deepcaller
{
    public class DeepcallerMod : Mod
    {
        public static DeepcallerSettings Settings { get; private set; }

        public DeepcallerMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<DeepcallerSettings>();
            new Harmony("Nidelese.VPE.Deepcaller").PatchAll();
        }

        public override string SettingsCategory() => "Deepcaller";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);
            bool automaticBefore = Settings.automaticAbyssalMeditation;
            listing.CheckboxLabeled("Deepcaller_AutomaticMeditation".Translate(),
                ref Settings.automaticAbyssalMeditation,
                "Deepcaller_AutomaticMeditationHelp".Translate());
            if (automaticBefore != Settings.automaticAbyssalMeditation)
                EpiphanyGameComponent.Instance?.RebuildCandidates();
            listing.Gap();
            listing.Label("Deepcaller_MeditationLearningHelp".Translate());
            listing.End();
        }
    }

    public class DeepcallerSettings : ModSettings
    {
        public bool automaticAbyssalMeditation;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref automaticAbyssalMeditation, "automaticAbyssalMeditation", false);
        }
    }

    [DefOf]
    public static class Deepcaller_DefOf
    {
        public static JobDef Deepcaller_ConsumeCorpse;
        public static ThingDef Deepcaller_Idol;
        public static HediffDef Deepcaller_Grasped;
        public static HediffDef Deepcaller_Riptiden;
        public static HediffDef Deepcaller_Inked;
        public static VEF.Abilities.AbilityDef Deepcaller_DeepHoard;
        public static TraderKindDef Deepcaller_DeepTrader;
        public static HediffDef Deepcaller_Symbiote;
        public static VEF.Abilities.AbilityDef Deepcaller_Tideguard;
        public static HediffDef Deepcaller_TideguardShield;
        public static HediffDef Deepcaller_AbyssalSoftening;
        public static ThingDef Deepcaller_AbilityVisual;

        static Deepcaller_DefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(Deepcaller_DefOf));
    }
}
