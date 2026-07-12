using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Deepcaller
{
    /// Side-screen alert while any player idol is past its hunger grace:
    /// the bargain surcharge is active and climbing, and the player should
    /// hear about it before opening the trade dialog. Alerts are discovered
    /// by reflection; no registration needed.
    public class Alert_HungryGod : Alert
    {
        private readonly List<Thing> culprits = new List<Thing>();

        public Alert_HungryGod()
        {
            defaultPriority = AlertPriority.Medium;
        }

        public override string GetLabel() => "Deepcaller_HungryGodAlert".Translate();

        public override TaggedString GetExplanation()
        {
            var comp = culprits.Count > 0 ? culprits[0].TryGetComp<CompIdolDevotion>() : null;
            return "Deepcaller_HungryGodAlertDesc".Translate(
                (comp?.DaysSinceFed ?? 0f).ToString("0.#"),
                (comp?.PriceFactor ?? 1f).ToStringPercent());
        }

        public override AlertReport GetReport()
        {
            culprits.Clear();
            foreach (var map in Find.Maps)
                foreach (var thing in map.listerThings.ThingsOfDef(Deepcaller_DefOf.Deepcaller_Idol))
                    if (thing.Faction == Faction.OfPlayer
                        && thing.TryGetComp<CompIdolDevotion>() is { Hungry: true })
                        culprits.Add(thing);
            return AlertReport.CulpritsAre(culprits);
        }
    }
}
