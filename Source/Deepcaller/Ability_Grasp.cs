using Verse;

namespace Deepcaller
{
    /// Grasp scales with Idol devotion: the better the deep has been fed,
    /// the longer it holds and the more damage it takes to tear a victim
    /// free. Duration and grip strength knobs live in GraspExtension on the
    /// hediff def, next to the leash parameters.
    public class Ability_Grasp : VEF.Abilities.Ability
    {
        private GraspExtension GraspExt =>
            def.GetModExtension<VEF.Abilities.AbilityExtension_Hediff>()?.hediff
                ?.GetModExtension<GraspExtension>();

        private int Devotion => CompIdolDevotion.HighestDevotionLevel(pawn.Map, pawn.Faction);

        public override int GetDurationForPawn()
        {
            var duration = base.GetDurationForPawn();
            var ext = GraspExt;
            if (ext != null)
                duration = (int)(duration * (1f + ext.durationFactorPerDevotion * Devotion));
            return duration;
        }

        public override Hediff ApplyHediff(Pawn targetPawn, HediffDef hediffDef,
            BodyPartRecord bodyPart, int duration, float severity)
        {
            var hediff = base.ApplyHediff(targetPawn, hediffDef, bodyPart, duration, severity);
            var ext = GraspExt;
            if (hediff is Hediff_Grasped grasped && ext != null)
                grasped.gripStrength = 1f + ext.gripStrengthPerDevotion * Devotion;
            return hediff;
        }
    }
}
