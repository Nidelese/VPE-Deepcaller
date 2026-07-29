using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace Deepcaller
{
    /// <summary>
    /// The Leviathan's mantle is the creature's main body, not another limb.
    /// A tiny visual-altitude lift keeps it above linked arms and flowers
    /// when their cells overlap without changing pathing or anatomy.
    /// </summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.DrawPos), MethodType.Getter)]
    public static class Pawn_DrawPos_LeviathanHeadPriority
    {
        public static void Postfix(Pawn __instance, ref UnityEngine.Vector3 __result)
        {
            if (__instance?.def?.defName == "Deepcaller_LeviathanHead")
                __result.y += 0.035f;
        }
    }

    /// <summary>
    /// Final shadow boundary: both PawnRenderer shadow sources eventually call
    /// Graphic_Shadow.DrawWorker with the pawn. Suppress that draw for the
    /// production tentacle family even if another mod restores shadow data.
    /// </summary>
    [HarmonyPatch(typeof(Graphic_Shadow), nameof(Graphic_Shadow.DrawWorker))]
    public static class Graphic_Shadow_NoSproutShadow
    {
        public static bool Prefix(Thing thing) =>
            !(thing is Pawn pawn)
            || !DeepcallerRenderUtility.HasIntegratedRift(pawn);
    }

    /// <summary>
    /// Force dynamic pose selection at the actual animal body-node material
    /// boundary. This path always has both the pawn and facing, unlike several
    /// cached Graphic access paths used by the renderer.
    /// </summary>
    [HarmonyPatch(typeof(PawnRenderNodeWorker), "GetMaterial")]
    public static class PawnRenderNodeWorker_SproutPose
    {
        public static void Postfix(
            PawnRenderNode node,
            PawnDrawParms parms,
            ref UnityEngine.Material __result)
        {
            var pawn = parms.pawn;
            if (parms.Portrait || !(node is PawnRenderNode_Body) || __result == null)
                return;

            if (pawn?.def?.defName == "Deepcaller_Tentacle"
                || pawn?.def?.defName == "Deepcaller_LeviathanArm")
                __result = Graphic_Sprout.MaterialFor(pawn, parms.facing, __result);
            else if (pawn?.def?.defName == "Deepcaller_Bud")
                __result = Graphic_Bud.MaterialFor(pawn, parms.facing, __result);
            else if (pawn?.def?.defName == "Deepcaller_LeviathanHead")
                __result = Graphic_Leviathan.MaterialFor(
                    pawn, parms.facing, __result);
            else if (pawn?.def?.defName == "Deepcaller_LeviathanFlower")
                __result = Graphic_LeviathanFlower.MaterialFor(
                    pawn, parms.facing, __result);
        }
    }

    /// <summary>
    /// Sprout art includes a grounded dimensional rift, so RimWorld's offset
    /// pawn shadow reads as a second floating portal. Suppress both the race
    /// and body shadow draw for every stage using that art family.
    /// </summary>
    [HarmonyPatch(typeof(PawnRenderer), "DrawShadowInternal")]
    public static class PawnRenderer_NoSproutShadow
    {
        public static bool Prefix(Pawn ___pawn) =>
            !DeepcallerRenderUtility.HasIntegratedRift(___pawn);
    }

    /// <summary>
    /// Preserve vanilla AttackMelee (including threat/aggro semantics), but
    /// insert a real anticipation toil immediately before its attack loop for
    /// tentacles. Their underlying targeting and damage remain vanilla.
    /// </summary>
    [HarmonyPatch(typeof(JobDriver_AttackMelee), "MakeNewToils")]
    public static class JobDriver_AttackMelee_SproutAnticipation
    {
        public static IEnumerable<Toil> Postfix(
            IEnumerable<Toil> __result,
            JobDriver_AttackMelee __instance)
        {
            var pawn = __instance.pawn;
            bool isTentacle =
                pawn?.def?.defName == "Deepcaller_Tentacle"
                || pawn?.def?.defName == "Deepcaller_LeviathanArm";
            if (!isTentacle)
                return __result;

            return WithAnticipation(__result, pawn, __instance);
        }

        private static IEnumerable<Toil> WithAnticipation(
            IEnumerable<Toil> original,
            Pawn pawn,
            JobDriver_AttackMelee driver)
        {
            bool replaced = false;
            foreach (var toil in original)
            {
                // This vanilla overlay floats above an undrafted pawn when it
                // acquires a hostile target. It reads as a transient "hat" on
                // a tiny portal creature and adds nothing to its readability.
                if (toil.debugName == "ThrowColonistAttackingMote")
                    continue;

                // AttackMelee's first Never toil is FollowAndMeleeAttack. Its
                // vanilla implementation owns both pursuit and impact, so an
                // earlier inserted toil winds up before approaching. Replace
                // that single toil with the same pursuit plus adjacent windup.
                if (!replaced && toil.defaultCompleteMode == ToilCompleteMode.Never)
                {
                    replaced = true;
                    int strikeAt = -1;
                    bool waitingForCooldown = false;
                    var snap = Toils_Combat.FollowAndMeleeAttack(
                        TargetIndex.A,
                        TargetIndex.B,
                        () =>
                        {
                            var target = pawn.CurJob?.GetTarget(TargetIndex.A).Thing;
                            if (target == null || target.Destroyed)
                                return;

                            if (waitingForCooldown)
                            {
                                if (!(pawn.stances?.curStance is Stance_Cooldown))
                                    waitingForCooldown = false;
                                return;
                            }

                            if (strikeAt < 0)
                            {
                                pawn.pather.StopDead();
                                pawn.rotationTracker.FaceTarget(target);
                                pawn.TryGetComp<CompSproutAnimation>()?.BeginSnapAttack();
                                strikeAt = Find.TickManager.TicksGame + 18;
                                return;
                            }

                            pawn.pather.StopDead();
                            pawn.rotationTracker.FaceTarget(target);
                            if (Find.TickManager.TicksGame < strikeAt)
                                return;

                            strikeAt = -1;
                            if (pawn.meleeVerbs.TryMeleeAttack(target, pawn.CurJob.verbToUse))
                                waitingForCooldown = true;
                        });
                    // A normal melee toil can pursue indefinitely between
                    // think-tree evaluations. Leviathan arms instead lose the
                    // job the instant prey leaves their sector or the central
                    // body's finite extension envelope.
                    snap.FailOn(() =>
                    {
                        var anatomy =
                            pawn.TryGetComp<CompLeviathanPart>();
                        if (anatomy?.IsLinked != true || anatomy.IsCore)
                            return false;
                        var target =
                            pawn.CurJob?.GetTarget(TargetIndex.A).Thing;
                        return target != null
                               && !anatomy.CanPredate(target);
                    });
                    yield return snap.FailOnDespawnedOrNull(TargetIndex.A);
                    continue;
                }

                yield return toil;
            }
        }
    }

    /// The ink veil belongs to the deep: BlindSmoke's flat x0.7 accuracy
    /// factor is hardcoded per-shot in ShotReport, so the deep's own ranged
    /// summons (the Leviathan's sludge spitter) are exempted here. Both
    /// summon races carry TentacleRaceExtension.
    [HarmonyPatch(typeof(ShotReport), nameof(ShotReport.HitReportFor))]
    public static class ShotReport_HitReportFor_DeepSeesThroughInk
    {
        private static readonly AccessTools.StructFieldRef<ShotReport, float> GasFactor =
            AccessTools.StructFieldRefAccess<ShotReport, float>("factorFromCoveringGas");

        public static void Postfix(Thing caster, ref ShotReport __result)
        {
            if (caster is Pawn shooter
                && shooter.def.HasModExtension<TentacleRaceExtension>())
                GasFactor(ref __result) = 1f;
        }
    }

    /// Kills feed the killer's growth comp, scaled by victim body size.
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    public static class Pawn_Kill_FeedTentacle
    {
        public static void Postfix(Pawn __instance, DamageInfo? dinfo)
        {
            if (dinfo?.Instigator is Pawn killer && killer != __instance)
            {
                var growth = killer.TryGetComp<CompTentacleGrowth>();
                growth?.Feed(__instance.BodySize * growth.Props.killFeedFactor);
            }
        }
    }

    /// Taunt: AttackTargetFinder multiplies target scores by this factor, so
    /// boosting it makes enemies engage tentacles instead of ignoring them.
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.TargetPriorityFactor), MethodType.Getter)]
    public static class Pawn_TargetPriorityFactor_Taunt
    {
        public static void Postfix(Pawn __instance, ref float __result)
        {
            var ext = __instance.def.GetModExtension<TentacleRaceExtension>();
            if (ext != null)
                __result *= ext.targetPriorityFactor;
        }
    }
}
