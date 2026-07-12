namespace Deepcaller
{
    /// A tree node that is never cast: unlocking it is the effect (code
    /// elsewhere checks whether the caster has learned it). No gizmo —
    /// same pattern as VPE Ragnarok's passive skills.
    public class Ability_Passive : VEF.Abilities.Ability
    {
        public override bool ShowGizmoOnPawn() => false;
    }
}
