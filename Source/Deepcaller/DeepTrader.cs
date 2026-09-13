using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Deepcaller
{
    /// TraderKindDef.WillTrade only passes defs some StockGenerator
    /// handles; the deep's hoard can hold anything, so this one handles
    /// everything and stocks nothing (goods come from the hoard itself).
    public class StockGenerator_DeepHoard : StockGenerator
    {
        public override IEnumerable<Thing> GenerateThings(PlanetTile forTile, Faction faction = null) =>
            Enumerable.Empty<Thing>();

        public override bool HandlesThingDef(ThingDef thingDef) => true;
    }

    /// The deep sets its own prices: market value x PriceFactor (devotion
    /// curve + hunger surcharge), no negotiator-skill or faction math.
    /// Silver stays 1:1 so the payment side is untouched.
    [HarmonyPatch(typeof(Tradeable), nameof(Tradeable.GetPriceFor))]
    public static class Tradeable_GetPriceFor_Patch
    {
        public static bool Prefix(Tradeable __instance, ref float __result)
        {
            if (!(TradeSession.trader is CompIdolDevotion idol) || __instance.IsCurrency)
                return true;
            __result = System.Math.Max(1, __instance.BaseMarketValue * idol.PriceFactor);
            return false;
        }
    }
}
