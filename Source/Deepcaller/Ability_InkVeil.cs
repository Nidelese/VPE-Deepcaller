using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Deepcaller
{
    public class AbilityExtension_InkVeil : DefModExtension
    {
        // Linear devotion scaling for the cloud size: +1 radius per this
        // much devotion, capped.
        public float devotionPerBonusCell = 5f;
        public float maxBonusCells = 10f;

        // Hostiles caught in the bloom get the Deepcaller_Inked hediff
        // (Sight reduction -> worse shooting AND melee). Severity scales
        // linearly with devotion; it decays on its own (severityPerDay on
        // the hediff), so higher devotion is both blinder and longer.
        public float inkSeverityBase = 0.25f;
        public float inkSeverityPerDevotion = 0.01f;

        // The custom cloud accompanies the mechanically authoritative gas.
        public int visualDurationTicks = 900;
    }

    /// Blooms a cloud of abyssal ink (vanilla BlindSmoke gas) over the
    /// target area: shots through it lose accuracy (flat x0.7, hardcoded
    /// per gas type) and turrets can't lock through it. Hostiles caught in
    /// the bloom also get ink in their eyes — a Sight debuff scaling with
    /// idol devotion. Dissipates naturally like smoke.
    public class Ability_InkVeil : VEF.Abilities.Ability
    {
        private static readonly AbilityExtension_InkVeil DefaultExt = new AbilityExtension_InkVeil();

        private AbilityExtension_InkVeil Ext =>
            def.GetModExtension<AbilityExtension_InkVeil>() ?? DefaultExt;

        private float Devotion => CompIdolDevotion.HighestDevotion(pawn.MapHeld, pawn.Faction);

        public override float GetRadiusForPawn() =>
            base.GetRadiusForPawn()
            + Mathf.Min(Mathf.Floor(Devotion / Ext.devotionPerBonusCell), Ext.maxBonusCells);

        public override void Cast(params GlobalTargetInfo[] targets)
        {
            base.Cast(targets);
            var ext = Ext;
            var map = pawn.Map;
            var radius = GetRadiusForPawn();
            var severity = ext.inkSeverityBase + ext.inkSeverityPerDevotion * Devotion;

            foreach (var target in targets)
            {
                var center = target.Cell;
                GasUtility.AddGas(center, map, GasType.BlindSmoke, radius);
                Thing_AbilityVisual.SpawnInk(
                    map, center, radius, ext.visualDurationTicks);

                foreach (var victim in map.mapPawns.AllPawnsSpawned
                             .Where(p => DeepTargetUtility.IsCombatTarget(p, pawn) && p.Position.InHorDistOf(center, radius))
                             .ToList())
                {
                    var inked = victim.health.hediffSet.GetFirstHediffOfDef(Deepcaller_DefOf.Deepcaller_Inked);
                    if (inked != null)
                        inked.Severity = Mathf.Max(inked.Severity, severity);
                    else
                    {
                        inked = HediffMaker.MakeHediff(Deepcaller_DefOf.Deepcaller_Inked, victim);
                        inked.Severity = severity;
                        victim.health.AddHediff(inked);
                    }
                }
            }
        }
    }
}
