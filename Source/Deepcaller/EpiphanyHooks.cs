using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using VanillaPsycastsExpanded;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace Deepcaller
{
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.SpawnSetup))]
    public static class Epiphany_PawnSpawned
    {
        public static void Postfix(Pawn __instance) => EpiphanyGameComponent.Instance?.Refresh(__instance);
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.SetFaction))]
    public static class Epiphany_PawnFaction
    {
        public static void Postfix(Thing __instance)
        {
            if (__instance is Pawn pawn) EpiphanyGameComponent.Instance?.Refresh(pawn);
        }
    }

    [HarmonyPatch(typeof(Hediff_PsycastAbilities), nameof(Hediff_PsycastAbilities.UnlockMeditationFocus))]
    public static class Epiphany_FocusLearned
    {
        public static void Postfix(Hediff_PsycastAbilities __instance, MeditationFocusDef focus)
        {
            if (AbyssalMeditation.IsAbyssal(focus)) EpiphanyGameComponent.Instance?.RemoveCandidate(__instance.pawn);
        }
    }

    [HarmonyPatch(typeof(Hediff_PsycastAbilities), nameof(Hediff_PsycastAbilities.Reset))]
    public static class Epiphany_FocusReset
    {
        public static void Postfix(Hediff_PsycastAbilities __instance) => EpiphanyGameComponent.Instance?.Refresh(__instance.pawn);
    }

    [HarmonyPatch(typeof(JobDriver), nameof(JobDriver.Cleanup))]
    public static class Epiphany_ReflectionCompleted
    {
        public static void Prefix(JobDriver __instance, JobCondition condition)
        {
            var component = EpiphanyGameComponent.Instance;
            if (condition != JobCondition.Succeeded || component?.IsCandidate(__instance.pawn) != true) return;
            if (__instance.pawn.GetLord()?.LordJob is LordJob_Ritual) return;
            var name = __instance.job?.def?.defName;
            if (name == "Meditate" || name == "Pray" || name == "VisitGrave")
                component.NotifyReflection(__instance.pawn);
        }
    }

    // Called once on a completed ritual, after the game has gathered actual
    // attendance and before it clears that list. Canceled rituals skip it.
    [HarmonyPatch(typeof(LordJob_Ritual), "AddParticipantThoughts")]
    public static class Epiphany_RitualCompleted
    {
        public static void Postfix(Dictionary<Pawn, int> ___totalPresenceTmp)
        {
            var component = EpiphanyGameComponent.Instance;
            if (component?.AnyCandidates != true) return;
            foreach (var entry in ___totalPresenceTmp)
                if (entry.Value > 0) component.NotifyReflection(entry.Key);
        }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.TakeDamage))]
    public static class Epiphany_Combat
    {
        public struct CombatHit { public Pawn attacker, victim; }
        public static void Prefix(Thing __instance, DamageInfo dinfo, out CombatHit __state)
        {
            __state = default;
            if (EpiphanyGameComponent.Instance?.AnyCandidates != true || !dinfo.Def.harmsHealth
                || dinfo.Instigator is not Pawn attacker || !__instance.HostileTo(attacker)) return;
            __state.attacker = attacker;
            __state.victim = __instance as Pawn;
        }
        public static void Postfix(DamageWorker.DamageResult __result, CombatHit __state)
        {
            if (__state.attacker == null || __result == null || __result.totalDamageDealt <= 0) return;
            var component = EpiphanyGameComponent.Instance;
            int now = Find.TickManager.TicksGame;
            component?.NotifyCombat(__state.attacker, true, now);
            component?.NotifyCombat(__state.victim, false, now);
        }
    }
}
