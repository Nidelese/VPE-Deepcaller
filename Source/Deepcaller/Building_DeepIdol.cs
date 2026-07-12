using RimWorld;
using Verse;

namespace Deepcaller
{
    /// The idol survives nomadic play: when its map is discarded (abandoned
    /// settlement, departed gravship site), the god's state withdraws into
    /// the game component instead of vanishing with the map. The next Raise
    /// Idol cast anywhere restores it.
    public class Building_DeepIdol : Building
    {
        public override void Notify_MyMapRemoved()
        {
            base.Notify_MyMapRemoved();
            if (Faction != Faction.OfPlayer)
                return;
            var store = DeepcallerGameComponent.Instance;
            var comp = this.TryGetComp<CompIdolDevotion>();
            if (store != null && comp != null)
                comp.WithdrawTo(store);
        }
    }
}
