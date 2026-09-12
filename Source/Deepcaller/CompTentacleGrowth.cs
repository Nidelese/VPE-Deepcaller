using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Deepcaller
{
    public class CompProperties_TentacleGrowth : CompProperties
    {
        public List<float> stageThresholds = new List<float> { 2f, 6f, 15f };
        public float killFeedFactor = 1f;
        public float corpseFeedFactor = 1.5f;
        public float healPerBodySizeConsumed = 12f;
        public float acquireRadiusBase = 32f;
        public float acquireRadiusPerStage = 10f;

        public CompProperties_TentacleGrowth() => compClass = typeof(CompTentacleGrowth);
    }

    public partial class CompTentacleGrowth : ThingComp
    {
        private const long TicksPerBiologicalYear = 3600000L;

        private float fed;
        private int growthSchema = 1;

        public CompProperties_TentacleGrowth Props => (CompProperties_TentacleGrowth)props;

        public Pawn Pawn => (Pawn)parent;

        public int Stage
        {
            get
            {
                var stage = 0;
                foreach (var threshold in Props.stageThresholds)
                    if (fed >= threshold)
                        stage++;
                return stage;
            }
        }

        public bool FullyGrown => Stage >= Props.stageThresholds.Count;

        public float AcquireRadius => Props.acquireRadiusBase + Props.acquireRadiusPerStage * Stage;

        public void Feed(float points)
        {
            if (points <= 0f)
                return;

            var stageBefore = Stage;
            fed += points;
            if (Stage == stageBefore)
                return;

            // Evolution stages are life stages: the setter recalculates the
            // life stage index, which applies size/health/damage factors.
            Pawn.ageTracker.AgeBiologicalTicks = Stage * TicksPerBiologicalYear + 1000L;
            Pawn.Drawer?.renderer?.SetAllGraphicsDirty();
            Regenerate();
            // Tideguard shield (if granted) scales with stage.
            var shield = Pawn.health.hediffSet.GetFirstHediffOfDef(Deepcaller_DefOf.Deepcaller_TideguardShield);
            if (shield != null)
                shield.Severity = Stage + 1;
            if (Pawn.Spawned)
                MoteMaker.ThrowText(Pawn.DrawPos, Pawn.Map, "Deepcaller_GrowthText".Translate(), 3.65f);
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (growthSchema == 0)
            {
                int earnedStage = Mathf.Clamp((int)(Pawn.ageTracker.AgeBiologicalTicks / TicksPerBiologicalYear),
                    0, Props.stageThresholds.Count);
                fed = CultivationMath.ReconcileGrowth(fed, earnedStage, Props.stageThresholds);
                growthSchema = 1;
            }
            // Loading must reconcile the comp, body, graphics and shield without
            // granting the free regeneration that a newly earned stage gives.
            Pawn.ageTracker.AgeBiologicalTicks = Stage * TicksPerBiologicalYear + 1000L;
            var shield = Pawn.health.hediffSet.GetFirstHediffOfDef(Deepcaller_DefOf.Deepcaller_TideguardShield);
            if (shield != null) shield.Severity = Stage + 1;
            Pawn.Drawer?.renderer?.SetAllGraphicsDirty();
        }

        /// Advances a living tentacle to a minimum stage. This is used by
        /// Overgrowth; summoned tentacles always start as Sprouts.
        public void GrantStage(int stage)
        {
            stage = Mathf.Min(stage, Props.stageThresholds.Count);
            if (stage <= 0 || Stage >= stage)
                return;
            Feed(Props.stageThresholds[stage - 1] - fed);
        }

        /// Devouring flesh knits the tentacle back together.
        public void Heal(float amount)
        {
            if (amount <= 0f)
                return;

            var hediffs = Pawn.health.hediffSet.hediffs;
            for (var i = hediffs.Count - 1; i >= 0 && amount > 0f; i--)
            {
                if (hediffs[i] is Hediff_Injury injury)
                {
                    var heal = Mathf.Min(amount, injury.Severity);
                    injury.Heal(heal);
                    amount -= heal;
                }
            }
        }

        /// Evolving sheds the wounded husk: injuries close, lost parts regrow.
        private void Regenerate()
        {
            foreach (var missing in Pawn.health.hediffSet.GetMissingPartsCommonAncestors().ToList())
                Pawn.health.RestorePart(missing.Part);
            foreach (var injury in Pawn.health.hediffSet.hediffs.OfType<Hediff_Injury>().ToList())
                injury.Heal(injury.Severity);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref fed, "fed");
            Scribe_Values.Look(ref growthSchema, "growthSchema", 0);
            ExposeCombatData();
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (var gizmo in base.CompGetGizmosExtra())
                yield return gizmo;

            if (Pawn.Faction != Faction.OfPlayer)
                yield break;

            var component = Current.Game.GetComponent<DeepcallerGameComponent>();
            if (component == null)
                yield break;

            yield return new Command_Toggle
            {
                defaultLabel = "Deepcaller_EatCorpsesLabel".Translate(),
                defaultDesc = "Deepcaller_EatCorpsesDesc".Translate(),
                icon = ContentFinder<UnityEngine.Texture2D>.Get("UI_Deepcaller/EatCorpses"),
                isActive = () => component.tentaclesEatCorpses,
                toggleAction = () => component.tentaclesEatCorpses = !component.tentaclesEatCorpses,
            };
        }

        public override string CompInspectStringExtra()
        {
            if (FullyGrown)
                return "Deepcaller_GrowthMature".Translate(fed.ToString("0.##")) + "\n" + CombatDescription;
            return "Deepcaller_GrowthProgress".Translate(
                fed.ToString("0.##"), Props.stageThresholds[Stage].ToString("0.##")) + "\n" + CombatDescription;
        }
    }
}
