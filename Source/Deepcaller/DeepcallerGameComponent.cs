using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Deepcaller
{
    /// Applies queued collision damage one tick after it was earned.
    /// Riptide's slams happen inside the victim's own hediff tick, and
    /// killing a pawn mid-health-tick is off vanilla's trodden path —
    /// Pawn.Kill despawns, builds and places the corpse on a half-ticked
    /// health state, with silent corpse-destroy bailouts (a slammed bison
    /// vanished without a corpse in playtest). Deferring the damage one
    /// tick puts the kill on a clean stack, identical to a weapon hit.
    public class DeepcallerGameComponent : GameComponent, IThingHolder
    {
        private struct PendingHit
        {
            public Thing target;
            public float amount;
            public Pawn instigator;
            public Hediff_Riptiden riptide;
            public bool riptidePawnHit;
        }

        private static readonly List<PendingHit> pending = new List<PendingHit>();

        // Player toggle (gizmo on tentacles): whether tentacles hunt corpses
        // on their own. Idol offerings are claimed regardless — see
        // CompIdolDevotion.ClaimsCorpse.
        public bool tentaclesEatCorpses = true;

        // The withdrawn god (nomadic campaigns): when a map holding the idol
        // is abandoned, Building_DeepIdol stashes its state here instead of
        // letting the map discard it. The next Raise Idol restores it.
        // withdrawnDevotion < 0 means nothing is stashed.
        public double withdrawnDevotion = -1;
        public CultivationProgress cultivation = new CultivationProgress();
        public int withdrawnTicksSinceConsume;
        public int withdrawnBudDamageLevel;
        public int withdrawnBudFireRateLevel;
        public int withdrawnBudBoltSpeedLevel;
        private ThingOwner<Thing> withdrawnHoard;

        public bool HasWithdrawnGod => withdrawnDevotion >= 0f;
        public ThingOwner<Thing> WithdrawnHoard => withdrawnHoard;

        public static bool TentaclesEatCorpses =>
            Current.Game?.GetComponent<DeepcallerGameComponent>()?.tentaclesEatCorpses ?? true;

        public static DeepcallerGameComponent Instance =>
            Current.Game?.GetComponent<DeepcallerGameComponent>();

        public DeepcallerGameComponent(Game game)
        {
            withdrawnHoard = new ThingOwner<Thing>(this);
        }

        public IThingHolder ParentHolder => null;

        public ThingOwner GetDirectlyHeldThings() => withdrawnHoard;

        public void GetChildHolders(List<IThingHolder> outChildren) =>
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());

        public void ClearWithdrawnGod()
        {
            withdrawnDevotion = -1f;
            withdrawnTicksSinceConsume = 0;
            withdrawnBudDamageLevel = 0;
            withdrawnBudFireRateLevel = 0;
            withdrawnBudBoltSpeedLevel = 0;
            withdrawnHoard.ClearAndDestroyContents();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref tentaclesEatCorpses, "tentaclesEatCorpses", true);
            Scribe_Deep.Look(ref cultivation, "cultivation");
            Scribe_Values.Look(ref withdrawnDevotion, "withdrawnDevotion", -1.0);
            Scribe_Values.Look(ref withdrawnTicksSinceConsume, "withdrawnTicksSinceConsume");
            Scribe_Values.Look(ref withdrawnBudDamageLevel, "withdrawnBudDamageLevel");
            Scribe_Values.Look(ref withdrawnBudFireRateLevel, "withdrawnBudFireRateLevel");
            Scribe_Values.Look(ref withdrawnBudBoltSpeedLevel, "withdrawnBudBoltSpeedLevel");
            Scribe_Deep.Look(ref withdrawnHoard, "withdrawnHoard", this);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                withdrawnHoard ??= new ThingOwner<Thing>(this);
                cultivation ??= new CultivationProgress();
                cultivation.Import(BudUpgradeKind.Damage, withdrawnBudDamageLevel);
                cultivation.Import(BudUpgradeKind.FireRate, withdrawnBudFireRateLevel);
                cultivation.Import(BudUpgradeKind.BoltSpeed, withdrawnBudBoltSpeedLevel);
            }
        }

        public static void QueueDamage(Thing target, float amount, Pawn instigator)
        {
            if (target != null && amount > 0f)
                pending.Add(new PendingHit { target = target, amount = amount, instigator = instigator });
        }

        public static void QueueRiptideDamage(Pawn target, float amount, Pawn instigator)
        {
            if (amount > 0 && target != null && !target.Dead)
                pending.Add(new PendingHit { target = target, amount = amount, instigator = instigator, riptidePawnHit = true });
        }

        public static bool QueueRiptideImpact(Thing obstacle, float amount, Pawn instigator, Hediff_Riptiden drag)
        {
            if (drag == null || amount <= 0 || float.IsNaN(amount)
                || !RiptideImpactUtility.CanDamage(RiptideImpactUtility.Resolve(drag.pawn, obstacle, instigator))) return false;
            pending.Add(new PendingHit { target = obstacle, amount = amount, instigator = instigator, riptide = drag });
            return true;
        }

        public override void GameComponentTick()
        {
            if (pending.Count == 0)
                return;
            for (var i = 0; i < pending.Count; i++)
            {
                var hit = pending[i];
                if (!hit.target.Destroyed && hit.target.SpawnedOrAnyParentSpawned)
                {
                    if (hit.riptide != null) hit.riptide.ResolveObstacleImpact(hit.target, hit.amount);
                    else
                        hit.target.TakeDamage(new DamageInfo(DamageDefOf.Blunt, hit.amount, 0f, -1f, hit.instigator));
                }
            }
            pending.Clear();
        }

        public override void StartedNewGame() => pending.Clear();

        public override void LoadedGame()
        {
            pending.Clear();
            foreach (var map in Find.Maps)
                foreach (var thing in map.listerThings.ThingsOfDef(Deepcaller_DefOf.Deepcaller_Idol))
                    thing.TryGetComp<CompIdolDevotion>()?.ImportLegacyCultivation();
        }
    }
}
