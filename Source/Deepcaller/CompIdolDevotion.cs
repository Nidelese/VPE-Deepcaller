using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Deepcaller
{
    public class CompProperties_IdolDevotion : CompProperties
    {
        public float radius = 11.9f;
        public List<float> devotionThresholds = new List<float> { 5f, 15f, 30f };
        public int consumeIntervalTicks = 2500;
        public float devotionPerBodySize = 1f;

        // Ransom pricing: market value x curve(devotion). Fair at 1000
        // devotion, abusive below, favorable approaching 2000. Fallback
        // flat factor if no curve is set.
        public float hoardGreedFactor = 1.25f;
        public SimpleCurve hoardPriceFactorByDevotion;

        // A hungry god drives harder bargains: after this many days
        // without a meal, the price factor climbs per additional day.
        public float hungerGraceDays = 3f;
        public float hungerPenaltyPerDay = 0.25f;

        // Devotion thresholds for the god's mood words (Disdainful below
        // the first, then Dismissive/Bored/Curious/Pleased, Proud past the
        // last). Cinematic stand-in for the exact price factor.
        public List<float> moodThresholds = new List<float> { 100f, 500f, 1000f, 1500f, 2000f };

        public CompProperties_IdolDevotion() => compClass = typeof(CompIdolDevotion);
    }

    public class CompIdolDevotion : ThingComp, IThingHolder, ITrader
    {
        private float devotion;
        private int ticksSinceConsume;

        // The deep's hoard: gear swallowed by Consume (with the Deep Hoard
        // passive learned), ransomable back for silver.
        private ThingOwner<Thing> hoard;

        public CompIdolDevotion() => hoard = new ThingOwner<Thing>(this);

        public ThingOwner<Thing> Hoard => hoard;

        /// Re-raising the idol moves the god, not replaces it: the freshly
        /// spawned idol inherits everything, the old shell is vanished by
        /// the caller (Ability_RaiseIdol).
        public void TransplantFrom(CompIdolDevotion other)
        {
            devotion = other.devotion;
            ticksSinceConsume = other.ticksSinceConsume;
            other.hoard.TryTransferAllToContainer(hoard);
        }

        /// The idol's map is being discarded (nomadic campaign moved on):
        /// stash the god's state game-wide instead of losing it with the
        /// map. Keeps the best god if several idols sink at once.
        public void WithdrawTo(DeepcallerGameComponent store)
        {
            if (devotion < store.withdrawnDevotion)
                return;
            store.withdrawnDevotion = devotion;
            store.withdrawnTicksSinceConsume = ticksSinceConsume;
            hoard.TryTransferAllToContainer(store.WithdrawnHoard);
        }

        public void RestoreFrom(DeepcallerGameComponent store)
        {
            devotion = store.withdrawnDevotion;
            ticksSinceConsume = store.withdrawnTicksSinceConsume;
            store.WithdrawnHoard.TryTransferAllToContainer(hoard);
            store.ClearWithdrawnGod();
        }

        public ThingOwner GetDirectlyHeldThings() => hoard;

        public void GetChildHolders(List<IThingHolder> outChildren) =>
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());

        public CompProperties_IdolDevotion Props => (CompProperties_IdolDevotion)props;

        public float Devotion => devotion;

        /// Current ransom price factor: devotion curve plus hunger surcharge.
        public float PriceFactor
        {
            get
            {
                var factor = Props.hoardPriceFactorByDevotion?.Evaluate(devotion)
                             ?? Props.hoardGreedFactor;
                var hungryDays = ticksSinceConsume / (float)GenDate.TicksPerDay - Props.hungerGraceDays;
                if (hungryDays > 0f)
                    factor += Props.hungerPenaltyPerDay * hungryDays;
                return factor;
            }
        }

        private static readonly string[] MoodKeys =
        {
            "Deepcaller_MoodDisdainful", "Deepcaller_MoodDismissive", "Deepcaller_MoodBored",
            "Deepcaller_MoodCurious", "Deepcaller_MoodPleased", "Deepcaller_MoodProud",
        };

        private string MoodWord
        {
            get
            {
                var tier = 0;
                foreach (var threshold in Props.moodThresholds)
                    if (devotion >= threshold)
                        tier++;
                return MoodKeys[Mathf.Min(tier, MoodKeys.Length - 1)].Translate();
            }
        }

        public bool Hungry =>
            ticksSinceConsume / (float)GenDate.TicksPerDay > Props.hungerGraceDays;

        public float DaysSinceFed => ticksSinceConsume / (float)GenDate.TicksPerDay;

        public int DevotionLevel
        {
            get
            {
                var level = 0;
                foreach (var threshold in Props.devotionThresholds)
                    if (devotion >= threshold)
                        level++;
                return level;
            }
        }

        /// Highest devotion level among a faction's idols on this map; sets the
        /// starting stage of newly summoned tentacles.
        public static int HighestDevotionLevel(Map map, Faction faction)
        {
            var best = 0;
            foreach (var thing in map.listerThings.ThingsOfDef(Deepcaller_DefOf.Deepcaller_Idol))
                if (thing.Faction == faction && thing.TryGetComp<CompIdolDevotion>() is { } comp)
                    best = Mathf.Max(best, comp.DevotionLevel);
            return best;
        }

        /// The idol wins the corpse contest: any non-mechanoid corpse inside
        /// a friendly idol's radius is a claimed offering, off-limits to
        /// tentacle auto-consumption.
        public static bool ClaimsCorpse(Corpse corpse, Faction faction)
        {
            if (corpse?.Map == null || corpse.InnerPawn.RaceProps.IsMechanoid)
                return false;
            foreach (var thing in corpse.Map.listerThings.ThingsOfDef(Deepcaller_DefOf.Deepcaller_Idol))
            {
                if (thing.Faction == faction
                    && thing.TryGetComp<CompIdolDevotion>() is { } comp
                    && corpse.Position.InHorDistOf(thing.Position, comp.Props.radius))
                    return true;
            }
            return false;
        }

        public static bool AnyIdol(Map map, Faction faction)
        {
            if (map == null)
                return false;
            foreach (var thing in map.listerThings.ThingsOfDef(Deepcaller_DefOf.Deepcaller_Idol))
                if (thing.Faction == faction)
                    return true;
            return false;
        }

        /// The faction's highest-devotion idol on this map: the mouth the
        /// deep prefers. Remote offerings and swallowed gear both go here.
        public static CompIdolDevotion BestIdol(Map map, Faction faction)
        {
            CompIdolDevotion best = null;
            if (map == null)
                return null;
            foreach (var thing in map.listerThings.ThingsOfDef(Deepcaller_DefOf.Deepcaller_Idol))
                if (thing.Faction == faction && thing.TryGetComp<CompIdolDevotion>() is { } comp
                    && (best == null || comp.devotion > best.devotion))
                    best = comp;
            return best;
        }

        /// A living sacrifice offered from afar (Consume): feeds the best
        /// idol directly, no corpse, no hauling.
        public static bool OfferRemote(Map map, Faction faction, float bodySizeEquivalent)
        {
            var best = BestIdol(map, faction);
            if (best == null)
                return false;

            var gained = bodySizeEquivalent * best.Props.devotionPerBodySize;
            best.devotion += gained;
            best.ticksSinceConsume = 0; // a living sacrifice is a meal
            MoteMaker.ThrowText(best.parent.DrawPos, map,
                "Deepcaller_OfferingTaken".Translate(gained.ToString("0.#")), 3.65f);
            return true;
        }

        /// Highest raw devotion among a faction's idols on this map; scales
        /// Riptide (range/radius/collision damage) and gates its casting.
        public static float HighestDevotion(Map map, Faction faction)
        {
            var best = 0f;
            if (map == null)
                return best;
            foreach (var thing in map.listerThings.ThingsOfDef(Deepcaller_DefOf.Deepcaller_Idol))
                if (thing.Faction == faction && thing.TryGetComp<CompIdolDevotion>() is { } comp)
                    best = Mathf.Max(best, comp.devotion);
            return best;
        }

        public override void CompTickRare()
        {
            base.CompTickRare();
            ticksSinceConsume += GenTicks.TickRareInterval;
            if (ticksSinceConsume < Props.consumeIntervalTicks)
                return;

            var corpse = FindOffering();
            if (corpse == null)
                return;

            ticksSinceConsume = 0;
            var gained = corpse.InnerPawn.BodySize * Props.devotionPerBodySize;
            devotion += gained;
            FilthMaker.TryMakeFilth(corpse.Position, parent.Map, ThingDefOf.Filth_Blood, 2);
            MoteMaker.ThrowText(corpse.DrawPos, parent.Map,
                "Deepcaller_OfferingTaken".Translate(gained.ToString("0.#")), 3.65f);
            corpse.Destroy();
        }

        private Corpse FindOffering()
        {
            foreach (var thing in parent.Map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse))
            {
                if (thing is Corpse corpse
                    && corpse.Position.InHorDistOf(parent.Position, Props.radius)
                    && !corpse.InnerPawn.RaceProps.IsMechanoid)
                    return corpse;
            }

            return null;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref devotion, "devotion");
            Scribe_Values.Look(ref ticksSinceConsume, "ticksSinceConsume");
            Scribe_Deep.Look(ref hoard, "hoard", this);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && hoard == null)
                hoard = new ThingOwner<Thing>(this);
        }

        /// The deep does not refund on demolition, but it does spit.
        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);
            hoard.TryDropAll(parent.Position, previousMap, ThingPlaceMode.Near);
        }

        public override void PostDrawExtraSelectionOverlays()
        {
            base.PostDrawExtraSelectionOverlays();
            GenDraw.DrawRadiusRing(parent.Position, Props.radius);
        }

        public override string CompInspectStringExtra()
        {
            var line = "Deepcaller_DevotionInspect".Translate(
                devotion.ToString("0.#"), DevotionLevel);
            if (DevotionLevel < Props.devotionThresholds.Count)
                line += " (" + "Deepcaller_DevotionNext".Translate(
                    Props.devotionThresholds[DevotionLevel].ToString("0.#")) + ")";
            line += "\n" + "Deepcaller_GodMood".Translate(MoodWord,
                (Hungry ? "Deepcaller_MoodHungry" : "Deepcaller_MoodSated").Translate());
            if (hoard.Count > 0)
                line += "\n" + "Deepcaller_HoardInspect".Translate(hoard.Count);
            return line;
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (var gizmo in base.CompGetGizmosExtra())
                yield return gizmo;

            yield return new Command_Action
            {
                defaultLabel = "Deepcaller_CorpseStockpileLabel".Translate(),
                defaultDesc = "Deepcaller_CorpseStockpileDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get("UI_Deepcaller/Abilities/RaiseIdol"),
                action = CreateCorpseStockpile,
            };

            yield return new Command_Action
            {
                defaultLabel = "Deepcaller_BargainLabel".Translate(),
                defaultDesc = "Deepcaller_BargainDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get("UI_Deepcaller/Abilities/Consume"),
                action = OpenBargainMenu,
            };

            if (DebugSettings.ShowDevGizmos)
            {
                var devotionIcon = ContentFinder<Texture2D>.Get("UI_Deepcaller/Abilities/LeviathansWake");
                yield return new Command_Action
                {
                    defaultLabel = "DEV: Devotion +5",
                    icon = devotionIcon,
                    action = () => devotion += 5f,
                };
                yield return new Command_Action
                {
                    defaultLabel = "DEV: Devotion +100",
                    icon = devotionIcon,
                    action = () => devotion += 100f,
                };
                yield return new Command_Action
                {
                    defaultLabel = "DEV: Devotion 0",
                    icon = devotionIcon,
                    action = () => devotion = 0f,
                };
            }
        }

        // --- ITrader: the vanilla trade dialog is the bargain UI. Prices
        // come from Tradeable_GetPriceFor_Patch (PriceFactor), payment is
        // silver lying within the idol's shadow.

        public TraderKindDef TraderKind => Deepcaller_DefOf.Deepcaller_DeepTrader;

        public IEnumerable<Thing> Goods => hoard;

        public int RandomPriceFactorSeed => parent.thingIDNumber;

        public string TraderName => "Deepcaller_TraderName".Translate();

        public bool CanTradeNow => hoard.Count > 0;

        public float TradePriceImprovementOffsetForPlayer => 0f;

        public Faction Faction => null;

        public TradeCurrency TradeCurrency => TradeCurrency.Silver;

        public IEnumerable<Thing> ColonyThingsWillingToBuy(Pawn playerNegotiator)
        {
            foreach (var thing in parent.Map.listerThings.ThingsOfDef(ThingDefOf.Silver))
                if (thing.Position.InHorDistOf(parent.Position, Props.radius))
                    yield return thing;
        }

        /// Payment swallowed whole. The deep does not make change twice.
        public void GiveSoldThingToTrader(Thing toGive, int countToGive, Pawn playerNegotiator)
        {
            toGive.SplitOff(countToGive).Destroy();
            FleckMaker.ThrowDustPuffThick(parent.DrawPos, parent.Map, 2f, DeepPullUtility.DeepColor);
        }

        public void GiveSoldThingToPlayer(Thing toGive, int countToGive, Pawn playerNegotiator)
        {
            var sold = toGive.SplitOff(countToGive);
            GenPlace.TryPlaceThing(sold, parent.Position, parent.Map, ThingPlaceMode.Near);
        }

        private void OpenBargainMenu()
        {
            if (hoard.Count == 0)
            {
                Messages.Message("Deepcaller_BargainEmpty".Translate(), parent,
                    MessageTypeDefOf.RejectInput, historical: false);
                return;
            }
            var negotiator = parent.Map.mapPawns.FreeColonistsSpawned.FirstOrDefault(p => !p.Downed);
            if (negotiator == null)
            {
                Messages.Message("Deepcaller_BargainNoNegotiator".Translate(), parent,
                    MessageTypeDefOf.RejectInput, historical: false);
                return;
            }
            Find.WindowStack.Add(new Dialog_Trade(negotiator, this));
        }

        /// Harbinger-tree trick: zone the idol's radius as a corpse stockpile so
        /// vanilla hauling delivers the offerings.
        private void CreateCorpseStockpile()
        {
            var map = parent.Map;
            var stockpile = new Zone_Stockpile(StorageSettingsPreset.CorpseStockpile, map.zoneManager);
            stockpile.settings.filter.SetAllow(ThingCategoryDefOf.CorpsesMechanoid, allow: false);
            map.zoneManager.RegisterZone(stockpile);

            foreach (var cell in GenRadial.RadialCellsAround(parent.Position, Props.radius, useCenter: false))
            {
                if (cell.InBounds(map)
                    && map.zoneManager.ZoneAt(cell) == null
                    && Designator_ZoneAdd.IsZoneableCell(cell, map))
                    stockpile.AddCell(cell);
            }

            if (stockpile.Cells.Count == 0)
                stockpile.Delete();
        }
    }

    /// Meditation focus strength that scales with the idol's devotion: the
    /// more the god has eaten, the more it has to give back. Plugs into
    /// vanilla CompMeditationFocus via the offsets list.
    public class FocusStrengthOffset_IdolDevotion : FocusStrengthOffset
    {
        public SimpleCurve offsetByDevotion;

        public override float GetOffset(Thing parent, Pawn user = null) =>
            offsetByDevotion?.Evaluate(
                parent.TryGetComp<CompIdolDevotion>()?.Devotion ?? 0f) ?? 0f;

        public override bool CanApply(Thing parent, Pawn user = null) =>
            parent.Spawned && offsetByDevotion != null;

        public override float MaxOffset(Thing parent = null) =>
            offsetByDevotion?.Max(p => p.y) ?? 0f;

        public override string GetExplanation(Thing parent) =>
            "Deepcaller_MeditationDevotion".Translate() + ": "
            + GetOffset(parent).ToStringWithSign("0%");

        public override string GetExplanationAbstract(ThingDef def = null) =>
            "Deepcaller_MeditationDevotion".Translate() + ": +0%-"
            + MaxOffset().ToStringPercent();
    }
}
