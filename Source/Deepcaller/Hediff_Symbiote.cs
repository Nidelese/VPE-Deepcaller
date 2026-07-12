using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Deepcaller
{
    public class SymbioteExtension : DefModExtension
    {
        // Manipulation points regained per day as the pawn grows into each
        // graft (from the -10 starting shock toward the graft's max).
        public float adaptPerDay = 2f;
    }

    /// Grafted tentacles of the deep. Each graft carries its own max
    /// manipulation bonus (set from idol devotion at cast time) and its
    /// own adaptation progress: fresh grafts are a -10 shock the pawn
    /// slowly grows into. The total shows up as a single dynamic
    /// Manipulation offset.
    public class Hediff_Symbiote : HediffWithComps
    {
        private const int UpdateInterval = 2500;

        private List<float> maxBonuses = new List<float>();
        private List<float> bonuses = new List<float>();
        private int ticksUntilUpdate;
        private HediffStage cachedStage;

        public int GraftCount => maxBonuses.Count;

        public float TotalBonus
        {
            get
            {
                var total = 0f;
                foreach (var bonus in bonuses)
                    total += bonus;
                return total;
            }
        }

        public void AddGraft(float maxBonus, float startingBonus)
        {
            maxBonuses.Add(maxBonus);
            bonuses.Add(startingBonus);
            cachedStage = null;
            pawn.health.Notify_HediffChanged(this);
        }

        public override HediffStage CurStage
        {
            get
            {
                if (cachedStage == null)
                {
                    cachedStage = new HediffStage
                    {
                        capMods = new List<PawnCapacityModifier>
                        {
                            new PawnCapacityModifier
                            {
                                capacity = PawnCapacityDefOf.Manipulation,
                                offset = TotalBonus / 100f,
                            },
                        },
                    };
                }
                return cachedStage;
            }
        }

        public override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            ticksUntilUpdate -= delta;
            if (ticksUntilUpdate > 0)
                return;
            ticksUntilUpdate = UpdateInterval;

            var adaptPerDay = def.GetModExtension<SymbioteExtension>()?.adaptPerDay ?? 2f;
            var step = adaptPerDay * UpdateInterval / GenDate.TicksPerDay;
            var changed = false;
            for (var i = 0; i < bonuses.Count; i++)
            {
                if (bonuses[i] >= maxBonuses[i])
                    continue;
                bonuses[i] = Mathf.Min(bonuses[i] + step, maxBonuses[i]);
                changed = true;
            }
            if (changed)
            {
                cachedStage = null;
                pawn.health.Notify_HediffChanged(this);
            }
        }

        public override string LabelInBrackets =>
            $"x{GraftCount}, {TotalBonus:+0.#;-0.#;0} manipulation";

        public override string TipStringExtra
        {
            get
            {
                var lines = base.TipStringExtra;
                for (var i = 0; i < bonuses.Count; i++)
                    lines += $"Graft {i + 1}: {bonuses[i]:+0.#;-0.#;0} / {maxBonuses[i]:+0.#;-0.#;0}\n";
                return lines;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref maxBonuses, "maxBonuses", LookMode.Value);
            Scribe_Collections.Look(ref bonuses, "bonuses", LookMode.Value);
            Scribe_Values.Look(ref ticksUntilUpdate, "ticksUntilUpdate");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                maxBonuses ??= new List<float>();
                bonuses ??= new List<float>();
            }
        }
    }
}
