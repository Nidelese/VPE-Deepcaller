using RimWorld;
using UnityEngine;
using Verse;

namespace Deepcaller
{
    public class LeviathanGlobExtension : DefModExtension
    {
        // Pawns do not have one conventional hit-point bar. Summing the
        // maximum health of their body parts gives the glob a stable
        // creature-scale value that also grows against large modded bosses.
        public float maxHealthFraction = 0.04f;
        public int minimumDamage = 9;
        public HediffDef resistanceHediff;
        public float resistanceSeverityPerHit = 0.2f;
        public int resistanceDurationTicks = 900;
    }

    /// <summary>
    /// The head's sludge scales against what it actually hits, rather than
    /// what the turret originally aimed at. That keeps misses and intercepted
    /// shots honest while preserving RimWorld's normal projectile resolution.
    /// </summary>
    public class Projectile_LeviathanGlob : Bullet
    {
        private int dynamicDamage;

        private LeviathanGlobExtension Extension =>
            def.GetModExtension<LeviathanGlobExtension>();

        public override int DamageAmount =>
            dynamicDamage > 0 ? dynamicDamage : base.DamageAmount;

        protected override void Impact(
            Thing hitThing,
            bool blockedByShield = false)
        {
            var extension = Extension;
            dynamicDamage = DamageFor(hitThing, extension);
            base.Impact(hitThing, blockedByShield);

            if (blockedByShield || extension?.resistanceHediff == null
                || hitThing is not Pawn pawn || pawn.Dead)
                return;

            var existing = pawn.health.hediffSet.GetFirstHediffOfDef(
                extension.resistanceHediff);
            if (existing == null)
            {
                existing = pawn.health.AddHediff(extension.resistanceHediff);
                existing.Severity = extension.resistanceSeverityPerHit;
            }
            else
            {
                existing.Severity = Mathf.Min(
                    existing.def.maxSeverity,
                    existing.Severity + extension.resistanceSeverityPerHit);
            }

            var disappears = existing.TryGetComp<HediffComp_Disappears>();
            if (disappears != null)
                disappears.ticksToDisappear = extension.resistanceDurationTicks;
        }

        private static int DamageFor(
            Thing target,
            LeviathanGlobExtension extension)
        {
            var minimum = Mathf.Max(1, extension?.minimumDamage ?? 9);
            var fraction = Mathf.Max(
                0f, extension?.maxHealthFraction ?? 0.04f);
            if (target == null)
                return minimum;

            float maxHealth;
            if (target is Pawn pawn)
            {
                maxHealth = 0f;
                foreach (var part in pawn.RaceProps.body.AllParts)
                    maxHealth += part.def.GetMaxHealth(pawn);
            }
            else
            {
                maxHealth = target.MaxHitPoints;
            }

            return Mathf.Max(minimum, Mathf.RoundToInt(maxHealth * fraction));
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref dynamicDamage, "dynamicDamage");
        }
    }

    public class LeviathanStunExtension : DefModExtension
    {
        public int stunTicks = 60;
    }

    /// <summary>
    /// Every flower owns its own turret and projectile, so all three select
    /// targets independently. A shielded impact remains shielded: the stun is
    /// applied only after an unblocked hit.
    /// </summary>
    public class Projectile_LeviathanStun : Bullet
    {
        protected override void Impact(
            Thing hitThing,
            bool blockedByShield = false)
        {
            base.Impact(hitThing, blockedByShield);
            if (blockedByShield || hitThing is not Pawn pawn || pawn.Dead)
                return;

            var ticks = Mathf.Max(
                1, def.GetModExtension<LeviathanStunExtension>()?.stunTicks ?? 60);
            pawn.stances?.stunner?.StunFor(
                ticks, launcher, addBattleLog: false);
        }
    }
}
