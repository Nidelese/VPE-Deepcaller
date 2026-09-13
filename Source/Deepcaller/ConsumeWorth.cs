using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using Verse;

namespace Deepcaller
{
    public static class ConsumeWorth
    {
        private static readonly Dictionary<Type, FieldInfo> levelFields = new Dictionary<Type, FieldInfo>();
        public static int Level(Pawn victim)
        {
            int level = 1;
            foreach (var comp in victim.AllComps)
            {
                var type = comp.GetType();
                if (type.FullName != "IsekaiLeveling.IsekaiComponent"
                    && type.FullName != "IsekaiLeveling.MobRanking.MobRankComponent") continue;
                if (!levelFields.TryGetValue(type, out var field))
                    levelFields[type] = field = type.GetField("currentLevel", BindingFlags.Public | BindingFlags.Instance);
                if (field?.GetValue(comp) is int current) level = Math.Max(level, current);
            }
            return level;
        }

        public static double MarketValue(Pawn victim)
        {
            double value = victim.MarketValue;
            if (victim.equipment != null)
                foreach (var gear in victim.equipment.AllEquipmentListForReading) value += gear.MarketValue * gear.stackCount;
            if (victim.apparel != null)
                foreach (var gear in victim.apparel.WornApparel) value += gear.MarketValue * gear.stackCount;
            if (victim.inventory != null)
                foreach (var gear in victim.inventory.innerContainer) value += gear.MarketValue * gear.stackCount;
            return value;
        }
    }
}
