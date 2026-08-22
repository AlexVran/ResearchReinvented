using HarmonyLib;
using PeteTimesSix.ResearchReinvented.Defs;
using PeteTimesSix.ResearchReinvented.Domain;
using PeteTimesSix.ResearchReinvented.Domain.Execution;
using PeteTimesSix.ResearchReinvented.Domain.State;
using PeteTimesSix.ResearchReinvented.Managers;
using PeteTimesSix.ResearchReinvented.Opportunities;
using PeteTimesSix.ResearchReinvented.OpportunityComps;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace PeteTimesSix.ResearchReinvented.Rimworld
{
    public static class ActivityHandlerIds
    {
        public static readonly ActivityHandlerId Theory = BuiltInActivityHandlerIds.Theory;
        public static readonly ActivityHandlerId AnalysisBench = BuiltInActivityHandlerIds.AnalysisBench;
        public static readonly ActivityHandlerId AnalysisFieldThing = BuiltInActivityHandlerIds.AnalysisFieldThing;
        public static readonly ActivityHandlerId AnalysisFieldTerrain = BuiltInActivityHandlerIds.AnalysisFieldTerrain;
        public static readonly ActivityHandlerId Social = BuiltInActivityHandlerIds.Social;
        public static readonly ActivityHandlerId MedicineTend = BuiltInActivityHandlerIds.MedicineTend;
        public static readonly ActivityHandlerId MedicineSurgery = BuiltInActivityHandlerIds.MedicineSurgery;
        public static readonly ActivityHandlerId Ingest = BuiltInActivityHandlerIds.Ingest;
        public static readonly ActivityHandlerId ObserveIngest = BuiltInActivityHandlerIds.ObserveIngest;
        public static readonly ActivityHandlerId Books = BuiltInActivityHandlerIds.Books;
        public static readonly ActivityHandlerId Tooling = BuiltInActivityHandlerIds.Tooling;
    }

    /// <summary>
    /// RimWorld adapter over the pure activity registry. Stable keys are resolved
    /// back to legacy projections only at this boundary so Phase 10 UI and Phase
    /// 11 prototypes can remain unchanged.
    /// </summary>
    public sealed class ResearchExecutionService
    {
        private readonly ResearchOpportunityManager owner;
        private readonly OpportunityActivityRegistry registry = new OpportunityActivityRegistry();
        private readonly Dictionary<ActivityHandlerId, HandlerCapability> handlerCapabilities = new Dictionary<ActivityHandlerId, HandlerCapability>();
        private readonly Dictionary<OpportunityKey, ResearchOpportunity> projections = new Dictionary<OpportunityKey, ResearchOpportunity>();
        private readonly OpportunityQueryIndex<Map, MapQueryKey, ResearchOpportunity> mapIndex = new OpportunityQueryIndex<Map, MapQueryKey, ResearchOpportunity>();
        private readonly HashSet<string> loggedUnavailableHandlers = new HashSet<string>(StringComparer.Ordinal);

        internal ResearchExecutionService(ResearchOpportunityManager owner)
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            Register(ActivityHandlerIds.Theory, HandlingMode.Job_Theory, typeof(JobDrivers.JobDriver_ResearchRR));
            Register(ActivityHandlerIds.AnalysisBench, HandlingMode.Job_Analysis, typeof(JobDrivers.JobDriver_Analyse));
            Register(ActivityHandlerIds.AnalysisFieldThing, HandlingMode.Job_Analysis, typeof(JobDrivers.JobDriver_AnalyseInPlace));
            Register(ActivityHandlerIds.AnalysisFieldTerrain, HandlingMode.Job_Analysis, typeof(JobDrivers.JobDriver_AnalyseTerrain));
            Register(ActivityHandlerIds.Social, HandlingMode.Social);
            Register(ActivityHandlerIds.MedicineTend, HandlingMode.Special_Medicine, startupCheck: () => TargetsAvailable(
                AccessTools.Method(typeof(TendUtility), nameof(TendUtility.DoTend))));
            Register(ActivityHandlerIds.MedicineSurgery, HandlingMode.Special_Medicine, startupCheck: () => TargetsAvailable(
                AccessTools.Method(typeof(SurgeryOutcomeComp_MedicineQuality), "XGetter")));
            Register(ActivityHandlerIds.Ingest, HandlingMode.Special_OnIngest, startupCheck: () => TargetsAvailable(
                AccessTools.Method(typeof(Thing), "PrePostIngested")));
            Register(ActivityHandlerIds.ObserveIngest, HandlingMode.Special_OnIngest_Observable, startupCheck: () => TargetsAvailable(
                AccessTools.Method(typeof(Recipe_AdministerIngestible), nameof(Recipe_AdministerIngestible.ApplyOnPawn))));
            Register(ActivityHandlerIds.Books, HandlingMode.Special_Books, startupCheck: () => TargetsAvailable(
                AccessTools.Field(typeof(ReadingOutcomeDoerGainResearch), "values"),
                AccessTools.Method(typeof(ReadingOutcomeDoerGainResearch), "IsProjectVisible"),
                AccessTools.Method(typeof(ReadingOutcomeDoerGainResearch), nameof(ReadingOutcomeDoerGainResearch.OnReadingTick))));
            Register(ActivityHandlerIds.Tooling, HandlingMode.Special_Tooling, startupCheck: () => TargetsAvailable(
                AccessTools.Method(typeof(Toils_Recipe), nameof(Toils_Recipe.DoRecipeWork))));
        }

        public OpportunityActivityRegistry Registry => registry;
        public int MapIndexRevision => mapIndex.Revision;

        internal void RunStartupChecks()
        {
            registry.RunStartupChecks();
            foreach (var diagnostic in registry.Diagnostics)
                Log.Warning($"RR activity handler {diagnostic.HandlerId} unavailable: {diagnostic.Reason}");
        }

        internal void SynchronizeProject(ResearchProjectDef project, IEnumerable<ResearchOpportunity> current)
        {
            foreach (var key in projections.Where(item => item.Value.project == project).Select(item => item.Key).ToArray())
                projections.Remove(key);
            foreach (var opportunity in current.Where(item => item != null && item.AuthoritativeKey != null).OrderBy(item => item.AuthoritativeKey))
                projections[opportunity.AuthoritativeKey] = opportunity;
            InvalidateMapIndexes();
        }

        internal void RemoveProject(ResearchProjectDef project)
        {
            foreach (var key in projections.Where(item => item.Value.project == project).Select(item => item.Key).ToArray())
                projections.Remove(key);
            InvalidateMapIndexes();
        }

        internal void Reset()
        {
            projections.Clear();
            InvalidateMapIndexes();
        }

        public void InvalidateMapIndexes()
        {
            mapIndex.InvalidateAll();
        }

        internal IReadOnlyList<string> UnavailabilityReasonsFor(ResearchOpportunity opportunity)
        {
            if (opportunity == null)
                return Array.Empty<string>();
            return registry.Diagnostics
                .Where(diagnostic => handlerCapabilities.TryGetValue(diagnostic.HandlerId, out var capability)
                    && capability.Supports(opportunity))
                .Select(diagnostic => $"{diagnostic.HandlerId} ({diagnostic.Status}): {diagnostic.Reason}")
                .Distinct()
                .OrderBy(detail => detail, StringComparer.Ordinal)
                .ToArray();
        }

        public IReadOnlyList<ResearchOpportunity> QueryCurrent(
            ActivityHandlerId handlerId,
            OpportunityAvailability? availability = null,
            object subject = null)
        {
            owner.CheckForRegeneration();
            var project = ResearchRuntimeServices.Current.CurrentResearchProject;
            return project == null ? Array.Empty<ResearchOpportunity>() : QueryProject(handlerId, project, availability, subject);
        }

        public IReadOnlyList<ResearchOpportunity> QueryProject(
            ActivityHandlerId handlerId,
            ResearchProjectDef project,
            OpportunityAvailability? availability = null,
            object subject = null)
        {
            if (project == null) return Array.Empty<ResearchOpportunity>();
            owner.EnsureGeneratedForExecution(project);
            var identity = ResearchOpportunityManager.IdentityFor<ResearchProjectDef>(project);
            var result = registry.Query(handlerId, owner.OpportunityService, identity, item =>
            {
                if (!projections.TryGetValue(item.Spec.Key, out var opportunity)) return false;
                if (availability.HasValue && (availability.Value & opportunity.CurrentAvailability) == 0) return false;
                return SubjectMatches(opportunity, subject);
            });
            if (!result.IsHandlerAvailable)
                LogUnavailable(result);
            return result.Keys.Select(key => projections.TryGetValue(key, out var opportunity) ? opportunity : null)
                .Where(opportunity => opportunity != null)
                .ToArray();
        }

        public ResearchOpportunity FindCurrent(ActivityHandlerId handlerId, OpportunityAvailability availability, object subject = null)
        {
            return QueryCurrent(handlerId, availability, subject).FirstOrDefault();
        }

        public IReadOnlyList<ResearchOpportunity> QueryForMap(Map map, ActivityHandlerId handlerId, Def subject)
        {
            if (map == null || subject == null) return Array.Empty<ResearchOpportunity>();
            var project = ResearchRuntimeServices.Current.CurrentResearchProject;
            if (project == null) return Array.Empty<ResearchOpportunity>();
            owner.CheckForRegeneration();
            var query = new MapQueryKey(handlerId, SubjectIdentity(subject));
            var candidates = mapIndex.GetOrAdd(map, query, () => QueryProject(handlerId, project, null, subject));
            return candidates.Where(opportunity => opportunity.CurrentAvailability == OpportunityAvailability.Available).ToArray();
        }

        public bool ApplyTick(ResearchOpportunity opportunity, float amount, Pawn researcher, int tickDelta = 1, int moteModulo = 600)
        {
            if (opportunity == null || researcher == null) return false;
            if (researcher.WorkTypeIsDisabled(WorkTypeDefOf.Research))
            {
                Log.Warning($"RR: Pawn {researcher} tried to do research tick despite being incapable of research");
                return false;
            }

            researcher.skills.Learn(SkillDefOf.Intellectual, 0.1f * tickDelta);
            var tickAmount = amount * tickDelta;
            float? moteAmount = researcher.IsHashIntervalTick(moteModulo) ? (int)(amount * moteModulo) : (float?)null;
            return Apply(opportunity, researcher, tickAmount, moteAmount, null, 0f);
        }

        public bool ApplyChunk(ResearchOpportunity opportunity, Pawn researcher, float amount, float modifier, float xp, string moteSubjectName = null, float moteOffsetHint = 0f)
        {
            if (opportunity == null || researcher == null) return false;
            if (researcher.WorkTypeIsDisabled(WorkTypeDefOf.Research))
            {
                Log.Warning($"RR: Pawn {researcher} tried to do research chunk despite being incapable of research");
                return false;
            }

            researcher.skills.Learn(SkillDefOf.Intellectual, xp);
            var startAmount = amount;
            amount = Math.Min(amount * modifier, opportunity.MaximumProgress);
            if (ResearchReinvented_Debug.debugPrintouts)
                Log.Message($"performing research chunk for {opportunity.ShortDesc}: modifier: {modifier} startAmount: {startAmount} amount {amount} ({amount * opportunity.def.GetCategory(opportunity.relation).Settings.researchSpeedMultiplier} after speedmult) (of {opportunity.MaximumProgress})");
            return Apply(opportunity, researcher, amount, amount, moteSubjectName, moteOffsetHint);
        }

        public bool FinishImmediately(ResearchOpportunity opportunity)
        {
            return opportunity != null && Apply(opportunity, null, opportunity.MaximumProgress, null, null, 0f);
        }

        private bool Apply(ResearchOpportunity opportunity, Pawn researcher, float amount, float? moteAmount, string moteSubjectName, float moteOffsetHint)
        {
            if (opportunity.AuthoritativeKey == null)
            {
                Log.WarningOnce($"RR: opportunity {opportunity} has no authoritative key; progress was not applied.", opportunity.loadID);
                return false;
            }

            if (moteAmount.HasValue)
                moteAmount = OpportunityProgressMath.ClampMoteAmount(moteAmount.Value, opportunity.Progress, opportunity.MaximumProgress);
            var techCostFactor = researcher?.Faction == null ? 1f : opportunity.project.CostFactor(researcher.Faction.def.techLevel);
            amount = OpportunityProgressMath.CalculateAppliedAmount(
                Math.Max(0f, amount), opportunity.Progress, opportunity.MaximumProgress,
                Find.Storyteller.difficulty.researchSpeedFactor,
                opportunity.def.GetCategory(opportunity.relation).Settings.researchSpeedMultiplier,
                techCostFactor,
                DebugSettings.fastResearch);
            var applied = owner.OpportunityService.ApplyProgress(opportunity.AuthoritativeKey, amount);
            if (applied <= 0f)
                moteAmount = null;
            if (researcher != null)
            {
                researcher.records.AddTo(RecordDefOf.ResearchPointsResearched, applied);
                if (ResearchRuntimeServices.Current.Settings.showProgressMotes && moteAmount.HasValue)
                    opportunity.DoResearchProgressMote(researcher, moteAmount.Value, moteSubjectName, moteOffsetHint);
            }

            var total = ResearchRuntimeServices.Current.ResearchManager.GetProgress(opportunity.project) + applied;
            ResearchManagerAccess.Field_progress[opportunity.project] = total;
            if (opportunity.project.IsFinished)
                owner.FinishProject(opportunity.project, true, researcher);
            return opportunity.project.IsFinished || opportunity.IsFinished;
        }

        private void Register(ActivityHandlerId id, HandlingMode mode, Type driverClass = null, Func<ActivityHandlerSelfCheck> startupCheck = null)
        {
            registry.Register(new RuntimeActivityHandler(id, this, mode, driverClass, startupCheck));
            handlerCapabilities[id] = new HandlerCapability(mode, driverClass);
        }

        private static ActivityHandlerSelfCheck TargetsAvailable(params object[] targets)
        {
            return targets != null && targets.All(target => target != null)
                ? ActivityHandlerSelfCheck.Ready()
                : ActivityHandlerSelfCheck.Unavailable("A required optional or version-sensitive RimWorld target is unavailable.");
        }

        private bool Supports(OpportunityKey key, HandlingMode mode, Type driverClass)
        {
            if (!projections.TryGetValue(key, out var opportunity) || !opportunity.def.handledBy.HasFlag(mode))
                return false;
            return driverClass == null || opportunity.JobDefs.Any(job => job.driverClass == driverClass);
        }

        private void LogUnavailable(ActivityQueryResult result)
        {
            var token = result.HandlerId + "|" + result.Status + "|" + result.UnavailableReason;
            if (loggedUnavailableHandlers.Add(token))
                Log.Warning($"RR activity {result.HandlerId} is unavailable ({result.Status}): {result.UnavailableReason}");
        }

        private static bool SubjectMatches(ResearchOpportunity opportunity, object subject)
        {
            if (subject == null) return true;
            if (subject is Faction faction)
                return opportunity.requirement is ROComp_RequiresFaction requiresFaction && requiresFaction.MetByFaction(faction);
            if (subject is Thing thing)
                return opportunity.requirement.MetBy(thing);
            if (subject is Def def)
                return opportunity.requirement.MetBy(def);
            return false;
        }

        private static string SubjectIdentity(object subject)
        {
            if (subject is Def def) return def.GetType().Name + "/" + def.defName;
            if (subject is Thing thing) return "ThingDef/" + thing.def.defName;
            if (subject is Faction faction) return "Faction/" + (faction.def?.defName ?? faction.GetUniqueLoadID());
            return subject.GetType().FullName + "/" + subject.GetHashCode();
        }

        private sealed class RuntimeActivityHandler : IOpportunityActivityHandler
        {
            private readonly ResearchExecutionService owner;
            private readonly HandlingMode mode;
            private readonly Type driverClass;
            private readonly Func<ActivityHandlerSelfCheck> startupCheck;

            public RuntimeActivityHandler(ActivityHandlerId id, ResearchExecutionService owner, HandlingMode mode, Type driverClass, Func<ActivityHandlerSelfCheck> startupCheck)
            {
                Id = id;
                this.owner = owner;
                this.mode = mode;
                this.driverClass = driverClass;
                this.startupCheck = startupCheck ?? ActivityHandlerSelfCheck.Ready;
            }

            public ActivityHandlerId Id { get; }
            public ActivityHandlerSelfCheck StartupCheck() => startupCheck();
            public IEnumerable<OpportunityKey> Select(ActivityHandlerQuery query) => query.Specifications
                .Where(item => owner.Supports(item.Spec.Key, mode, driverClass))
                .Select(item => item.Spec.Key);
        }

        private sealed class HandlerCapability
        {
            private readonly HandlingMode mode;
            private readonly Type driverClass;

            internal HandlerCapability(HandlingMode mode, Type driverClass)
            {
                this.mode = mode;
                this.driverClass = driverClass;
            }

            internal bool Supports(ResearchOpportunity opportunity)
            {
                if (opportunity?.def == null || !opportunity.def.handledBy.HasFlag(mode))
                    return false;
                return driverClass == null || opportunity.JobDefs.Any(job => job.driverClass == driverClass);
            }
        }

        private sealed class MapQueryKey : IEquatable<MapQueryKey>
        {
            public MapQueryKey(ActivityHandlerId handlerId, string subject)
            {
                HandlerId = handlerId;
                Subject = subject;
            }

            public ActivityHandlerId HandlerId { get; }
            public string Subject { get; }
            public bool Equals(MapQueryKey other) => other != null && HandlerId == other.HandlerId && string.Equals(Subject, other.Subject, StringComparison.Ordinal);
            public override bool Equals(object obj) => Equals(obj as MapQueryKey);
            public override int GetHashCode() => (HandlerId.GetHashCode() * 397) ^ StringComparer.Ordinal.GetHashCode(Subject);
        }
    }
}
