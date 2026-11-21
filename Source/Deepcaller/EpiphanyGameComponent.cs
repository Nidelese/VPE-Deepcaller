using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Deepcaller
{
    public class EpiphanyPawnState : IExposable
    {
        public Pawn pawn;
        public bool accepted, refused, pending;
        public int sleepCycle = int.MinValue;
        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref accepted, "accepted");
            Scribe_Values.Look(ref refused, "refused");
            Scribe_Values.Look(ref pending, "pending");
            Scribe_Values.Look(ref sleepCycle, "sleepCycle", int.MinValue);
        }
    }

    public class EpiphanyCombatState : IExposable
    {
        public Pawn source, summoner;
        public int lastCombatTick = -1;
        public bool rolled;
        public void ExposeData()
        {
            Scribe_References.Look(ref source, "source");
            Scribe_References.Look(ref summoner, "summoner");
            Scribe_Values.Look(ref lastCombatTick, "lastCombatTick", -1);
            Scribe_Values.Look(ref rolled, "rolled");
        }
    }

    /// <summary>
    /// Only enrolled, locked pawns participate in event handling or sleep
    /// polling. Focus checks occur when enrolling or when unlocks change,
    /// never in the recurring epiphany paths.
    /// </summary>
    public class EpiphanyGameComponent : GameComponent
    {
        public const int CombatQuietTicks = 2500;
        private List<EpiphanyPawnState> states = new List<EpiphanyPawnState>();
        private List<EpiphanyCombatState> combat = new List<EpiphanyCombatState>();
        private readonly Dictionary<Pawn, EpiphanyPawnState> byPawn = new Dictionary<Pawn, EpiphanyPawnState>();
        private readonly Dictionary<Pawn, EpiphanyCombatState> bySource = new Dictionary<Pawn, EpiphanyCombatState>();
        private readonly HashSet<Pawn> candidates = new HashSet<Pawn>();
        private readonly List<Pawn> sleepSnapshot = new List<Pawn>();

        public EpiphanyGameComponent(Game game) { }
        public static EpiphanyGameComponent Instance => Current.Game?.GetComponent<EpiphanyGameComponent>();
        public bool AnyCandidates => candidates.Count > 0;
        public bool IsCandidate(Pawn pawn) => pawn != null && candidates.Contains(pawn);
        public bool HasRevelation(Pawn pawn) => pawn != null && byPawn.TryGetValue(pawn, out var state) && state.accepted;
        public static bool IsPlayerPawn(Pawn pawn) => pawn != null && !pawn.Destroyed && !pawn.Dead
            && pawn.RaceProps.Humanlike && pawn.health?.hediffSet != null && pawn.story != null && pawn.IsFreeColonist;

        private EpiphanyPawnState State(Pawn pawn)
        {
            if (byPawn.TryGetValue(pawn, out var found)) return found;
            var state = new EpiphanyPawnState { pawn = pawn };
            states.Add(state);
            byPawn.Add(pawn, state);
            return state;
        }

        public void Refresh(Pawn pawn)
        {
            if (pawn == null) return;
            candidates.Remove(pawn);
            if (!IsPlayerPawn(pawn) || DeepcallerMod.Settings?.automaticAbyssalMeditation == true) return;
            if (byPawn.TryGetValue(pawn, out var state) && (state.accepted || state.refused || state.pending)) return;
            var focus = DefDatabase<MeditationFocusDef>.GetNamedSilentFail("Deepcaller_Deep");
            if (focus == null || focus.CanPawnUse(pawn)) return;
            candidates.Add(pawn);
        }

        public void RebuildCandidates()
        {
            candidates.Clear();
            if (Current.Game == null || Find.Maps == null) return;
            foreach (var map in Find.Maps)
                foreach (var pawn in map.mapPawns.AllPawnsSpawned) Refresh(pawn);
        }

        public void RemoveCandidate(Pawn pawn) { if (pawn != null) candidates.Remove(pawn); }

        public static bool HasAffinity(Pawn pawn) => pawn?.Ideo?.memes?.Any(m =>
            m.defName == "VME_Structure_ChthonianCult" || m.defName == "VME_Structure_Esotericism") == true;

        public static int GraftCount(Pawn pawn) => (pawn?.health?.hediffSet
            ?.GetFirstHediffOfDef(Deepcaller_DefOf.Deepcaller_Symbiote) as Hediff_Symbiote)?.GraftCount ?? 0;

        // Noon-to-noon keeps a normal night's sleep on one roll even across
        // midnight. Day sleepers also get one opportunity per sleep cycle.
        public static int SleepCycle(Pawn pawn) => GenTicks.TicksAbs - GenLocalDate.DayTick(pawn)
            - (GenLocalDate.DayTick(pawn) < GenDate.TicksPerDay / 2 ? GenDate.TicksPerDay : 0);

        public override void GameComponentTick()
        {
            if (combat.Count > 0 && Find.TickManager.TicksGame % GenDate.TicksPerDay == 0)
                foreach (var old in combat.Where(s => s.source == null || s.source.Destroyed || s.source.Dead).ToArray())
                {
                    combat.Remove(old);
                    if (old.source != null) bySource.Remove(old.source);
                }
            if (candidates.Count == 0 || Find.TickManager.TicksGame % 250 != 0) return;
            sleepSnapshot.Clear();
            sleepSnapshot.AddRange(candidates);
            foreach (var pawn in sleepSnapshot)
            {
                if (!IsPlayerPawn(pawn)) { candidates.Remove(pawn); continue; }
                if (pawn.Spawned && !pawn.Downed && pawn.jobs?.curDriver?.asleep == true)
                    NotifySleep(pawn, SleepCycle(pawn));
            }
        }

        public void NotifySleep(Pawn pawn, int cycle)
        {
            if (!IsCandidate(pawn)) return;
            int grafts = GraftCount(pawn);
            bool affinity = HasAffinity(pawn);
            if (grafts == 0 && !affinity) return;
            var state = State(pawn);
            if (state.sleepCycle == cycle) return;
            state.sleepCycle = cycle;
            // Equivalent to an independent 5% roll for each attached graft.
            if (grafts > 0 && TryRoll(pawn, 1f - (float)Math.Pow(0.95, grafts), "Deepcaller_EpiphanyDream")) return;
            if (affinity) TryRoll(pawn, 0.005f, "Deepcaller_EpiphanyReflection");
        }

        public void NotifyReflection(Pawn pawn)
        {
            if (IsCandidate(pawn) && HasAffinity(pawn)) TryRoll(pawn, 0.005f, "Deepcaller_EpiphanyReflection");
        }

        public void BindSummon(Pawn source, Pawn summoner)
        {
            if (source == null || !IsCandidate(summoner)) return;
            var state = CombatState(source);
            state.summoner = summoner;
        }

        private EpiphanyCombatState CombatState(Pawn source)
        {
            if (bySource.TryGetValue(source, out var found)) return found;
            var state = new EpiphanyCombatState { source = source };
            combat.Add(state);
            bySource.Add(source, state);
            return state;
        }

        private Pawn Caller(Pawn source)
        {
            if (source == null) return null;
            if (bySource.TryGetValue(source, out var state) && state.summoner != null) return state.summoner;
            if (!source.def.HasModExtension<TentacleRaceExtension>()) return null;
            var core = source.TryGetComp<CompLeviathanCore>()
                ?? source.TryGetComp<CompLeviathanPart>()?.core?.TryGetComp<CompLeviathanCore>();
            return core?.summoner;
        }

        public void NotifyCombat(Pawn source, bool dealtDamage, int now)
        {
            if (candidates.Count == 0 || source == null) return;
            var caller = Caller(source);
            if (!IsCandidate(caller)) return;
            var state = CombatState(source);
            state.summoner = caller;
            if (state.lastCombatTick < 0 || now - state.lastCombatTick >= CombatQuietTicks) state.rolled = false;
            state.lastCombatTick = now;
            if (!dealtDamage || state.rolled) return;
            state.rolled = true;
            TryRoll(caller, 0.01f, "Deepcaller_EpiphanyCombat");
        }

        public static bool OfferingDispleases(Pawn pawn, Corpse corpse)
        {
            if (pawn?.Ideo == null || corpse?.InnerPawn == null) return false;
            // Use ideological reactions only: raw/rotten food disgust is not
            // an objection to the god feeding. The game's ingestion helpers
            // respect nullifying traits, genes, precepts and hediffs.
            return FoodUtility.ThoughtsFromIngesting(pawn, corpse, corpse.def).Any(t =>
                t.fromPrecept != null && t.thought.stages.Any(s => s.baseMoodEffect < 0)
                && ThoughtUtility.CanGetThought(pawn, t.thought, true));
        }

        public void NotifyCorpseEaten(Pawn tentacle, Corpse corpse)
        {
            if (candidates.Count == 0 || tentacle?.Spawned != true || corpse?.InnerPawn == null) return;
            // Check the small candidate roster, rather than every pawn on the
            // map. Only successful rolls need the more expensive ideology test.
            foreach (var pawn in candidates.ToArray())
                if (pawn.Spawned && pawn.Map == tentacle.Map
                    && pawn.Position.DistanceToSquared(tentacle.Position) <= 100)
                    TryRoll(pawn, 0.01f, "Deepcaller_EpiphanyOffering", () => !OfferingDispleases(pawn, corpse));
        }

        public bool TryRoll(Pawn pawn, float chance, string reason, Func<bool> allowed = null)
        {
            if (!IsCandidate(pawn) || !Rand.Chance(chance) || allowed != null && !allowed()) return false;
            var state = State(pawn);
            state.pending = true;
            candidates.Remove(pawn);
            var letter = (ChoiceLetter_AbyssalEpiphany)LetterMaker.MakeLetter(
                "Deepcaller_EpiphanyTitle".Translate(),
                "Deepcaller_EpiphanyText".Translate(pawn.Named("PAWN"), reason.Translate().Named("REASON")),
                DefDatabase<LetterDef>.GetNamed("Deepcaller_AbyssalEpiphany"), new LookTargets(pawn));
            letter.pawn = pawn;
            Find.LetterStack.ReceiveLetter(letter);
            return true;
        }

        public void ResolveInvitation(Pawn pawn, bool accept)
        {
            if (pawn == null) return;
            var state = State(pawn);
            state.pending = false;
            if (accept && IsPlayerPawn(pawn)) state.accepted = true;
            else state.refused = true;
            candidates.Remove(pawn);
            MeditationFocusTypeAvailabilityCache.ClearFor(pawn);
        }

        public override void StartedNewGame() => RebuildCandidates();
        public override void LoadedGame() => RebuildCandidates();
        public override void ExposeData()
        {
            Scribe_Collections.Look(ref states, "abyssalEpiphanies", LookMode.Deep);
            Scribe_Collections.Look(ref combat, "abyssalCombat", LookMode.Deep);
            if (Scribe.mode != LoadSaveMode.PostLoadInit) return;
            states ??= new List<EpiphanyPawnState>();
            combat ??= new List<EpiphanyCombatState>();
            byPawn.Clear();
            bySource.Clear();
            foreach (var state in states) if (state.pawn != null) byPawn[state.pawn] = state;
            foreach (var state in combat) if (state.source != null) bySource[state.source] = state;
        }
    }

    public class ChoiceLetter_AbyssalEpiphany : ChoiceLetter
    {
        public Pawn pawn;
        public override bool CanDismissWithRightClick => false;
        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                if (ArchivedOnly || !EpiphanyGameComponent.IsPlayerPawn(pawn)) { yield return Option_Close; yield break; }
                yield return new DiaOption("Deepcaller_EpiphanyAccept".Translate())
                {
                    action = () => Resolve(true), resolveTree = true
                };
                yield return new DiaOption("Deepcaller_EpiphanyRefuse".Translate())
                {
                    action = () => Resolve(false), resolveTree = true
                };
                yield return Option_Postpone;
            }
        }
        private void Resolve(bool accept)
        {
            EpiphanyGameComponent.Instance?.ResolveInvitation(pawn, accept);
            Find.LetterStack.RemoveLetter(this);
        }
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref pawn, "pawn");
        }
    }
}
