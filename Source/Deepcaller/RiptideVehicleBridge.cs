using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace Deepcaller
{
    // Optional integration: no Vehicles.dll is required to load Deepcaller.
    public static class RiptideVehicleBridge
    {
        private static readonly Type VehicleType = AccessTools.TypeByName("Vehicles.VehiclePawn");
        private static readonly Type StatType = AccessTools.TypeByName("Vehicles.VehicleStatDef");
        private static readonly MethodInfo GetStat = VehicleType == null || StatType == null ? null
            : AccessTools.Method(VehicleType, "GetStatValue", new[] { StatType });
        private static readonly Type StatDefsType = AccessTools.TypeByName("Vehicles.VehicleStatDefOf");
        private static readonly FieldInfo MassStat = StatDefsType == null ? null : AccessTools.Field(StatDefsType, "Mass");
        private static readonly MethodInfo Teleported = VehicleType == null ? null
            : AccessTools.Method(VehicleType, "Notify_Teleported", new[] { typeof(bool), typeof(bool) });
        private static readonly MethodInfo Reclaim = VehicleType == null ? null : AccessTools.Method(VehicleType, "ReclaimPosition");
        private static readonly FieldInfo Pather = VehicleType == null ? null : AccessTools.Field(VehicleType, "vehiclePather");
        private static readonly MethodInfo Stop = Pather == null ? null : AccessTools.Method(Pather.FieldType, "StopDead");
        private static readonly PropertyInfo Rotation = VehicleType == null ? null : AccessTools.Property(VehicleType, "FullRotation");
        private static readonly MethodInfo Footprint = VehicleType == null ? null : AccessTools.Method(VehicleType, "InhabitedCellsProjected");
        private static readonly PropertyInfo VehicleRotation = VehicleType == null ? null : AccessTools.Property(VehicleType, "Rotation");

        public static bool IsVehicle(Thing thing) => VehicleType?.IsInstanceOfType(thing) == true;

        public static void TurnAround(Thing thing)
        {
            var opposite = new Rot4((thing.Rotation.AsInt + 2) % 4);
            if (IsVehicle(thing) && VehicleRotation?.CanWrite == true)
            {
                VehicleRotation.SetValue(thing, opposite);
                Reclaim?.Invoke(thing, null);
            }
            else thing.Rotation = opposite;
        }

        public static double Mass(Pawn vehicle) => GetStat?.Invoke(vehicle, new[] { MassStat?.GetValue(null) }) is float mass
            ? mass : 70 * vehicle.BodySize;

        public static void Halt(Pawn pawn)
        {
            if (IsVehicle(pawn)) Stop?.Invoke(Pather?.GetValue(pawn), null);
            else pawn.pather?.StopDead();
        }

        public static void NotifyMoved(Pawn pawn)
        {
            if (!IsVehicle(pawn)) { pawn.Notify_Teleported(endCurrentJob: true, resetTweenedPos: true); return; }
            Teleported?.Invoke(pawn, new object[] { true, true });
            Reclaim?.Invoke(pawn, null);
        }

        public static IEnumerable<IntVec3> ProjectedCells(Thing body, IntVec3 next)
        {
            if (IsVehicle(body) && Footprint?.Invoke(body, new[] { (object)next, Rotation?.GetValue(body), 0 })
                    is IEnumerable<IntVec3> cells) return cells;
            return GenAdj.OccupiedRect(next, body.Rotation, body.def.size).Cells;
        }
    }
}
