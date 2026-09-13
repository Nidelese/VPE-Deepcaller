using System.Linq;
using RimWorld;
using Verse;

namespace Deepcaller
{
    public partial class CompIdolDevotion
    {
        public void StashGear(Pawn victim)
        {
            if (victim.equipment != null)
                foreach (var gear in victim.equipment.AllEquipmentListForReading.ToList())
                    gear.holdingOwner.TryTransferToContainer(gear, hoard);
            if (victim.apparel != null)
                foreach (var gear in victim.apparel.WornApparel.ToList())
                    gear.holdingOwner.TryTransferToContainer(gear, hoard);
            if (victim.inventory != null)
                foreach (var gear in victim.inventory.innerContainer.ToList())
                    gear.holdingOwner.TryTransferToContainer(gear, hoard);
        }

        public void RestorePurchase(Thing thing)
        {
            if (thing.def.useHitPoints)
                thing.HitPoints = HoardMath.RestoredHitPoints(thing.HitPoints, thing.MaxHitPoints,
                    Progress?.Rank(BudUpgradeKind.HoardRestoration) ?? 0);
            if ((Progress?.Rank(BudUpgradeKind.HoardCleansing) ?? 0) > 0 && thing is Apparel apparel)
                apparel.WornByCorpse = false;
        }
    }
}
