using PeteTimesSix.ResearchReinvented.Defs;
using PeteTimesSix.ResearchReinvented.Opportunities;
using PeteTimesSix.ResearchReinvented.OpportunityComps;
using PeteTimesSix.ResearchReinvented.Rimworld;
using PeteTimesSix.ResearchReinvented.Rimworld.WorkGivers;
using PeteTimesSix.ResearchReinvented.Utilities;
using PeteTimesSix.ResearchReinvented.Domain.State;
using PeteTimesSix.ResearchReinvented.Domain;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI;

namespace PeteTimesSix.ResearchReinvented.Managers
{
    public class ResearchOpportunityManager : GameComponent
    {
        public static ResearchOpportunityManager Instance => ResearchRuntimeServices.Current.GetGameComponent<ResearchOpportunityManager>();

        public int changeTicker = -1;
        public bool clearedThisTick = false;

        private List<ResearchOpportunity> _allGeneratedOpportunities = new List<ResearchOpportunity>();

        private readonly OpportunityService _opportunityService = new OpportunityService();
        internal IOpportunityService OpportunityService => _opportunityService;

        private readonly ResearchExecutionService _execution;
        public ResearchExecutionService Execution => _execution;

        [Unsaved(false)]
        private OpportunitySaveRootData _opportunitySaveRoot;


        public IReadOnlyCollection<ResearchOpportunity> AllGeneratedOpportunities => _allGeneratedOpportunities.AsReadOnly();

        private ResearchProjectDef _currentProject;
        public ResearchProjectDef CurrentProject => _currentProject;

        private List<ResearchOpportunity> _currentProjectOpportunitiesCache;
        public IReadOnlyCollection<ResearchOpportunity> CurrentProjectOpportunities
        {
            get
            {
                bool currentProjectRegenned = CheckForRegeneration();
                if(currentProjectRegenned || _currentProjectOpportunitiesCache == null) 
                {
                    _currentProjectOpportunitiesCache = AllGeneratedOpportunities.Where(o => o.IsValid() && o.project == _currentProject).ToList();
                }
                return _currentProjectOpportunitiesCache.AsReadOnly();
            }
        }
        private HashSet<ResearchOpportunityCategoryDef> _currentOpportunityCategoriesCache;
        public IReadOnlyCollection<ResearchOpportunityCategoryDef> CurrentProjectOpportunityCategories
        {
            get
            {
                bool currentProjectRegenned = CheckForRegeneration();
                if (currentProjectRegenned || _currentOpportunityCategoriesCache == null)
                {
                    _currentOpportunityCategoriesCache = CurrentProjectOpportunities.Select(o => o.def.GetCategory(o.relation)).ToHashSet();
                }
                return _currentOpportunityCategoriesCache.ToList().AsReadOnly();
            }
        }
        public HashSet<ResearchProjectDef> _projectsGenerated = new HashSet<ResearchProjectDef>();

        private List<ResearchOpportunityCategoryTotalsStore> _categoryStores = new List<ResearchOpportunityCategoryTotalsStore>();

        private bool regenerateWhenPossible = false;

        private Dictionary<ResearchProjectDef, Dictionary<ResearchOpportunityCategoryDef, OpportunityAvailability>> _categoryAvailability = new Dictionary<ResearchProjectDef, Dictionary<ResearchOpportunityCategoryDef, OpportunityAvailability>>();

        public ResearchOpportunityManager(Game game)
        {
            _execution = new ResearchExecutionService(this);
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            clearedThisTick = false;
            if (regenerateWhenPossible)
            {
                regenerateWhenPossible = false;
                GenerateOpportunities(ResearchRuntimeServices.Current.CurrentResearchProject, true);
            }
            CheckForRegeneration();
            //CancelMarkedPrototypes();
        }

        public override void GameComponentUpdate()
        {
            base.GameComponentUpdate();
            if (!Find.TickManager.Paused)
            {
                CheckForPopups();
            }
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            StartupChecks();
            _execution.RunStartupChecks();
        }

        public void StartupChecks() 
        {
            _projectsGenerated = _projectsGenerated ?? new HashSet<ResearchProjectDef>();
            _allGeneratedOpportunities = _allGeneratedOpportunities ?? new List<ResearchOpportunity>();
            _categoryAvailability = _categoryAvailability ?? new Dictionary<ResearchProjectDef, Dictionary<ResearchOpportunityCategoryDef, OpportunityAvailability>>();
            _categoryStores = _categoryStores ?? new List<ResearchOpportunityCategoryTotalsStore>();

            // Schema v1 intentionally saves semantic progress rather than the
            // generated specifications. Regeneration here is therefore a
            // schema requirement, not an ordinary-load progress reset.
            var saved = _opportunityService.CreateSnapshot(changeTicker);
            if (!_opportunityService.IsReadOnly && saved.RequiresSpecificationRegeneration)
            {
                foreach (var projectIdentity in saved.GeneratedProjects.OrderBy(identity => identity))
                {
                    var project = ResolveDef<ResearchProjectDef>(projectIdentity);
                    if (project != null)
                        GenerateOpportunities(project, false);
                    else
                        _opportunityService.RetainUnmatchedProject(projectIdentity, $"No loaded ResearchProjectDef named {projectIdentity.DefName} exists; its saved state remains orphaned.");
                }
            }
            var selected = ResearchRuntimeServices.Current.CurrentResearchProject;
            if (selected != null && !_opportunityService.IsReadOnly)
                GenerateOpportunities(selected, false);
            else
            {
                _currentProject = selected;
                if (!_opportunityService.IsReadOnly)
                    _opportunityService.ActiveProject = null;
            }

            //clear caches. TODO: centralize caches in here instead?
            CacheClearer.ClearCaches();
        }

        public bool CheckForRegeneration() 
        {
            if(ResearchRuntimeServices.Current.CurrentResearchProject != _currentProject)
            {
                PrototypeKeeper.Instance.CancelPrototypes(_currentProject, ResearchRuntimeServices.Current.CurrentResearchProject);
                GenerateOpportunities(ResearchRuntimeServices.Current.CurrentResearchProject, false);
                return true;
            }
            return false;
        }

        private long popupCheckTick = -1;
        public void CheckForPopups()
        {
            //for very low tickrate situations
            if (popupCheckTick == Find.TickManager.TicksGame)
                return;
            popupCheckTick = Find.TickManager.TicksGame;

            var currentProject = ResearchRuntimeServices.Current.CurrentResearchProject;
            if (currentProject == null)
                return;
            if (!_categoryAvailability.ContainsKey(currentProject))
                _categoryAvailability[currentProject] = new Dictionary<ResearchOpportunityCategoryDef, OpportunityAvailability>();
            var projectCategoryAvailability = _categoryAvailability[currentProject];

            foreach (var category in CurrentProjectOpportunityCategories)
            {
                var current = category.GetCurrentAvailability(ResearchRuntimeServices.Current.CurrentResearchProject);
                if (!projectCategoryAvailability.ContainsKey(category))
                {
                    projectCategoryAvailability[category] = current;
                }
                else if (projectCategoryAvailability[category] != current)
                {
                    projectCategoryAvailability[category] = current;
                    if(current == OpportunityAvailability.Available)
                    {
                        var toPopup = CurrentProjectOpportunities.Where(o => o.def.generatesPopups &&  o.def.GetCategory(o.relation) == category).ToList();
                        if (toPopup.Any())
                        {
                            var groups = toPopup.GroupBy(o => new { o.def, o.relation });
                            foreach(var group in groups)
                            {
                                bool tooLong = group.Count() > 5;
                                string label = group.Key.def.GetHeaderCap(group.Key.relation);
                                string msg;
                                if (tooLong)
                                    msg = "RR_opportunityTypeReady_Many".Translate(label, string.Join(", ", group.Take(5).Select(o => o.requirement.Subject)), group.Count() - 6);
                                else
                                    msg = "RR_opportunityTypeReady".Translate(label, string.Join(", ", group.Select(o => o.requirement.Subject)));
                                Messages.Message(msg, MessageTypeDefOf.TaskCompletion, historical: false);
                            }
                        }
                    }
                }
            }
        }

        public ResearchOpportunity GetFirstFilteredOpportunity(OpportunityAvailability? desiredAvailability, HandlingMode? handledBy)
        {
            return GetOpportunityFilter(desiredAvailability, handledBy, null);
        }

        /*public ResearchOpportunity GetFirstFilteredOpportunity(OpportunityAvailability? desiredAvailability, HandlingMode? handledBy, Type driverClass)
        {
            return GetOpportunityFilter(desiredAvailability, handledBy, (op) => op.JobDefs != null && op.JobDefs.Any(jd => jd.driverClass == driverClass));
        }*/

        public ResearchOpportunity GetFirstFilteredOpportunity(OpportunityAvailability? desiredAvailability, HandlingMode? handledBy, ResearchOpportunityCategoryDef category)
        {
            return GetOpportunityFilter(desiredAvailability, handledBy, (op) => op.def.GetCategory(op.relation) == category);
        }

        public ResearchOpportunity GetFirstFilteredOpportunity(OpportunityAvailability? desiredAvailability, HandlingMode? handledBy, Def def)
        {
            return GetOpportunityFilter(desiredAvailability, handledBy, (op) => op.requirement.MetBy(def));
        }

        public ResearchOpportunity GetFirstFilteredOpportunity(OpportunityAvailability? desiredAvailability, HandlingMode? handledBy, Thing thing)
        {
            return GetOpportunityFilter(desiredAvailability, handledBy, (op) => op.requirement.MetBy(thing));
        }

        public ResearchOpportunity GetFirstFilteredOpportunity(OpportunityAvailability? desiredAvailability, HandlingMode? handledBy, Faction faction)
        {
            return GetOpportunityFilter(desiredAvailability, handledBy, (op) => op.requirement is ROComp_RequiresFaction requiresFaction && requiresFaction.MetByFaction(faction));
        }

        public ResearchOpportunity GetFirstFilteredOpportunity(OpportunityAvailability? desiredAvailability, HandlingMode? handledBy, Func<ResearchOpportunity, bool> validator)
        {
            return GetOpportunityFilter(desiredAvailability, handledBy, validator);
        }

        private ResearchOpportunity GetOpportunityFilter(OpportunityAvailability? desiredAvailability, HandlingMode? handledBy, Func<ResearchOpportunity, bool> validator)
        {
            List<ResearchOpportunity> opportunities = new List<ResearchOpportunity>();
            foreach (var op in CurrentProjectOpportunities)
            {
                if (((!desiredAvailability.HasValue) || (desiredAvailability.Value & op.CurrentAvailability) != 0) &&
                    (!handledBy.HasValue || op.def.handledBy.HasFlag(handledBy.Value)) &&
                    (validator == null || validator(op)))
                    return op;
            }
            return null;
        }

        public List<ResearchOpportunity> GetFilteredOpportunities(OpportunityAvailability? desiredAvailability, HandlingMode? handledBy)
        {
            return GetOpportunitiesFilter(desiredAvailability, handledBy, null);
        }

        public List<ResearchOpportunity> GetFilteredOpportunities(OpportunityAvailability? desiredAvailability, HandlingMode? handledBy, Type driverClass)
        {
            return GetOpportunitiesFilter(desiredAvailability, handledBy, (op) => op.JobDefs != null && op.JobDefs.Any(jd => jd.driverClass == driverClass));
        }

        public List<ResearchOpportunity> GetFilteredOpportunities(OpportunityAvailability? desiredAvailability, HandlingMode? handledBy, ResearchOpportunityCategoryDef category)
        {
            return GetOpportunitiesFilter(desiredAvailability, handledBy, (op) => op.def.GetCategory(op.relation) == category);
        }

        public List<ResearchOpportunity> GetFilteredOpportunities(OpportunityAvailability? desiredAvailability, HandlingMode? handledBy, Def def)
        {
            return GetOpportunitiesFilter(desiredAvailability, handledBy, (op) => op.requirement.MetBy(def));
        }

        public List<ResearchOpportunity> GetFilteredOpportunities(OpportunityAvailability? desiredAvailability, HandlingMode? handledBy, Thing thing)
        {
            return GetOpportunitiesFilter(desiredAvailability, handledBy, (op) => op.requirement.MetBy(thing));
        }

        public List<ResearchOpportunity> GetFilteredOpportunities(OpportunityAvailability? desiredAvailability, HandlingMode? handledBy, Faction faction)
        {
            return GetOpportunitiesFilter(desiredAvailability, handledBy, (op) => op.requirement is ROComp_RequiresFaction requiresFaction && requiresFaction.MetByFaction(faction));
        }

        public List<ResearchOpportunity> GetFilteredOpportunities(OpportunityAvailability? desiredAvailability, HandlingMode? handledBy, Func<ResearchOpportunity, bool> validator)
        {
            return GetOpportunitiesFilter(desiredAvailability, handledBy, validator);
        }

        private List<ResearchOpportunity> GetOpportunitiesFilter(OpportunityAvailability? desiredAvailability, HandlingMode? handledBy, Func<ResearchOpportunity, bool> validator)
        {
            List<ResearchOpportunity> opportunities = new List<ResearchOpportunity>();
            foreach (var op in CurrentProjectOpportunities)
            {
                if (((!desiredAvailability.HasValue) || (desiredAvailability.Value & op.CurrentAvailability) != 0) &&
                    (!handledBy.HasValue || op.def.handledBy.HasFlag(handledBy.Value)) &&
                    (validator == null || validator(op)))
                opportunities.Add(op);
            }
            return opportunities;
        }

        public List<ResearchOpportunity> GetOpportunitiesFilterForProject(OpportunityAvailability? desiredAvailability, HandlingMode? handledBy, ResearchProjectDef project, Func<ResearchOpportunity, bool> validator)
        {
            List<ResearchOpportunity> opportunities = new List<ResearchOpportunity>();
            if (!_projectsGenerated.Contains(project))
                GenerateOpportunities(project, false);

            foreach (var op in AllGeneratedOpportunities)
            {
                if (project == op.project &&
                    (((!desiredAvailability.HasValue) || (desiredAvailability.Value & op.CurrentAvailability) != 0) &&
                    (!handledBy.HasValue || op.def.handledBy.HasFlag(handledBy.Value)) &&
                    (validator == null || validator(op))))
                    opportunities.Add(op);
            }
            return opportunities;
        }

        public List<ResearchOpportunity> GetOpportunitiesFilterForProjects(OpportunityAvailability? desiredAvailability, HandlingMode? handledBy, IEnumerable<ResearchProjectDef> projects, Func<ResearchOpportunity, bool> validator)
        {
            List<ResearchOpportunity> opportunities = new List<ResearchOpportunity>();
            foreach(var project in projects)
            {
                if (!_projectsGenerated.Contains(project))
                    GenerateOpportunities(project, false);
            }
            foreach (var op in AllGeneratedOpportunities)
            {
                if (projects.Contains(op.project) &&
                    (((!desiredAvailability.HasValue) || (desiredAvailability.Value & op.CurrentAvailability) != 0) &&
                    (!handledBy.HasValue || op.def.handledBy.HasFlag(handledBy.Value)) &&
                    (validator == null || validator(op))))
                    opportunities.Add(op);
            }
            return opportunities;
        }

        /*public IEnumerable<ResearchOpportunity> GetCurrentlyAvailableOpportunitiesFiltered(bool includeFinished, HandlingMode? handledBy, Func<ResearchOpportunity, bool> validator)
        {
            CheckForRegeneration();
            var ops = CurrentProjectOpportunities.Where(o => o.IsValid());
            if(includeFinished)
                ops = ops.Where(o => o.CurrentAvailability == OpportunityAvailability.Available);
            else
                ops = ops.Where(o => o.CurrentAvailability == OpportunityAvailability.Available || o.CurrentAvailability == OpportunityAvailability.Finished);
            if (handledBy.HasValue)
                ops = ops.Where(o => o.def.handledBy.HasFlag(handledBy));
            if (validator != null)
                ops = ops.Where(o => validator(o));

            return ops;
        }*/

        /*public IEnumerable<ResearchOpportunity> GetCurrentlyAvailableOpportunities(bool includeFinished = false)
        {
            bool currentProjectRegenned = CheckForRegeneration();
            if (!includeFinished)
                return CurrentProjectOpportunities.Where(o => o.IsValid() && o.CurrentAvailability == OpportunityAvailability.Available);
            else
                return CurrentProjectOpportunities.Where(o => o.IsValid() && (o.CurrentAvailability == OpportunityAvailability.Available || o.CurrentAvailability == OpportunityAvailability.Finished));
        }*/

        public ResearchOpportunityCategoryTotalsStore GetTotalsStore(ResearchProjectDef project, ResearchOpportunityCategoryDef category)
        {
            return _categoryStores.FirstOrDefault(cs => cs.project == project && cs.category == category);
        }

        internal float GetAuthoritativeCategoryProgress(ResearchProjectDef project, ResearchOpportunityCategoryDef category)
        {
            return _opportunityService.CategoryProgress(
                IdentityFor<ResearchProjectDef>(project),
                IdentityFor<ResearchOpportunityCategoryDef>(category));
        }

        public void PostFinishProject(ResearchProjectDef project)
        {
            _allGeneratedOpportunities.RemoveAll(o => o.project == project);
            _projectsGenerated.Remove(project);
            _categoryStores.RemoveAll(cs => cs.project == project);
            _opportunityService.RemoveProject(IdentityFor<ResearchProjectDef>(project));
            _execution.RemoveProject(project);

            if (_currentProject == project)
            {
                _currentProject = null;
                _currentProjectOpportunitiesCache?.Clear();
                _currentOpportunityCategoriesCache?.Clear();
            }
        }

        public void ResetAllProgress()
        {
            _allGeneratedOpportunities?.Clear();
            _currentProjectOpportunitiesCache?.Clear();
            _currentOpportunityCategoriesCache?.Clear();
            _categoryStores?.Clear();
            _projectsGenerated?.Clear();
            _opportunityService.Reset();
            _execution.Reset();
            clearedThisTick = true;
        }


        public void FinishProject(ResearchProjectDef project, bool doCompletionDialog = false, Pawn researcher = null)
        {
            ResearchRuntimeServices.Current.ResearchManager.FinishProject(project, doCompletionDialog, researcher);
        }

        public void DelayedRegeneration()
        {
            this.regenerateWhenPossible = true;
        }

        public void GenerateOpportunities(ResearchProjectDef project, bool forceRegen)
        {
            GenerateOpportunities(project, forceRegen, activate: true);
        }

        internal void EnsureGeneratedForExecution(ResearchProjectDef project)
        {
            if (project != null && !_projectsGenerated.Contains(project))
                GenerateOpportunities(project, false, activate: false);
        }

        private void GenerateOpportunities(ResearchProjectDef project, bool forceRegen, bool activate)
        {
            if (activate && _currentProject == project && !forceRegen)
            {
                return;
            }
            if (activate)
            {
                _currentProjectOpportunitiesCache = null;
                _currentOpportunityCategoriesCache = null;
                _currentProject = project;
            }
            if (project == null)
                return;
            if (_opportunityService.IsReadOnly)
                return;

            if (_projectsGenerated.Contains(project)) 
            {
                if (forceRegen)
                {
                    var preexistingOpportunities = _allGeneratedOpportunities.Where(o => o.project == project).ToList();
                    foreach (var preexisting in preexistingOpportunities)
                        _allGeneratedOpportunities.Remove(preexisting);
                }
                else
                {
                    if (activate)
                        _opportunityService.ActiveProject = IdentityFor<ResearchProjectDef>(project);
					if (ResearchReinvented_Debug.shadowComparisons && ResearchRuntimeServices.Current.CurrentResearchProject == project)
						ResearchShadowComparisonSession.Compare(
							project,
							_allGeneratedOpportunities.Where(opportunity => opportunity.IsValid() && opportunity.project == project).ToArray(),
							_categoryStores.Where(store => store.project == project).ToArray());
                    return;
                }
            }

            var generated = OpportunitySpecificationPipeline.Generate(project, ResearchRuntimeServices.Current);
            var projectIdentity = IdentityFor<ResearchProjectDef>(project);
            _opportunityService.SetSpecifications(
                projectIdentity,
                generated.Specifications,
                generated.Budgets);
            if (activate)
                _opportunityService.ActiveProject = projectIdentity;
            foreach (var projection in generated.Projections)
                projection.Legacy.BindAuthoritativeState(projection.Specification.Spec.Key);
            var newOpportunities = generated.Projections.Select(projection => projection.Legacy).ToList();
            var categoryStores = generated.Budgets.Select(budget => new ResearchOpportunityCategoryTotalsStore
            {
                project = project,
                category = ResolveDef<ResearchOpportunityCategoryDef>(budget.Category),
                researchPoints = budget.Budget,
            }).Where(store => store.category != null).ToList();
            _categoryStores.RemoveAll(cs => cs.project == project);
            _categoryStores.AddRange(categoryStores);

            var invalidOpportunities = newOpportunities.Where(o => !o.IsValid());
            if (invalidOpportunities.Any())
                Log.Warning($"Generated {invalidOpportunities.Count()} invalid opportunities for project {project}!");

            _allGeneratedOpportunities.AddRange(newOpportunities.Where(o => o.IsValid()));
            _projectsGenerated.Add(project);
            _execution.SynchronizeProject(project, newOpportunities.Where(o => o.IsValid()));

            foreach (var rejection in generated.Rejections.Take(32))
                Log.Warning($"RR state: specification projection rejected {rejection.Kind}: {rejection.Detail}");
            var suppressed = generated.SuppressedDiagnostics + Math.Max(0, generated.Rejections.Count - 32);
            if (suppressed > 0)
                Log.Warning($"RR state: suppressed {suppressed} additional generation/projection diagnostics for {project.defName}.");

			if (ResearchReinvented_Debug.shadowComparisons && ResearchRuntimeServices.Current.CurrentResearchProject == project)
				ResearchShadowComparisonSession.Compare(project, newOpportunities.Where(o => o.IsValid()).ToArray(), categoryStores);

            if (ResearchReinvented_Debug.debugPrintouts)
            {
                Log.Message($"Listing generated opportunities for project {project.label}...");
                foreach (var opportunity in newOpportunities)
                {
                    Log.Message($" |-- {opportunity.ShortDesc} -- {opportunity.debug_source} (imp.: {opportunity.importance})");
                }
            }

            _categoryAvailability.Clear();
            if(!_categoryAvailability.ContainsKey(project))
            {
                _categoryAvailability[project] = new Dictionary<ResearchOpportunityCategoryDef, OpportunityAvailability>();
            }
            var projectCategoryAvailability = _categoryAvailability[project];
            foreach (var category in newOpportunities.Select(opportunity => opportunity.def.GetCategory(opportunity.relation)).Where(category => category != null).Distinct())
            {
                projectCategoryAvailability[category] = category.GetCurrentAvailability(project);
            }
        }

		internal void RunShadowComparisonForCurrentProject()
		{
			if (_currentProject == null)
			{
				Log.Message("RR shadow: no current research project is selected.");
				return;
			}
			ResearchShadowComparisonSession.Compare(
				_currentProject,
				_allGeneratedOpportunities.Where(opportunity => opportunity.IsValid() && opportunity.project == _currentProject).ToArray(),
				_categoryStores.Where(store => store.project == _currentProject).ToArray());
		}

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref changeTicker, "changeTicker", -1);
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                if (_opportunityService.IsReadOnly)
                    throw new InvalidOperationException("Research Reinvented cannot overwrite opportunity state written by a newer schema. Load this save with the newer mod version before saving again.");
                _opportunitySaveRoot = new OpportunitySaveRootData(_opportunityService.CreateSnapshot(changeTicker));
            }
            Scribe_Deep.Look(ref _opportunitySaveRoot, "opportunityState");

            // Old field names are read only when the new root is absent. Once
            // migration succeeds, every subsequent save writes only schema v1.
            if (_opportunitySaveRoot == null && Scribe.mode != LoadSaveMode.Saving)
            {
                Scribe_Collections.Look(ref _allGeneratedOpportunities, "_allGeneratedOpportunities", LookMode.Deep);
                Scribe_Collections.Look(ref _projectsGenerated, "_allProjectsWithGeneratedOpportunities", LookMode.Def);
                Scribe_Defs.Look(ref _currentProject, "currentProject");
                Scribe_Collections.Look(ref _categoryStores, "_categoryStores", LookMode.Deep);
            }
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (_opportunitySaveRoot != null)
                    _opportunityService.Restore(_opportunitySaveRoot.ToDomain());
                else
                    MigrateLegacyState();
                _allGeneratedOpportunities = new List<ResearchOpportunity>();
                _projectsGenerated = new HashSet<ResearchProjectDef>();
                _categoryStores = new List<ResearchOpportunityCategoryTotalsStore>();
                _currentProject = null;
            }
        }

        private void MigrateLegacyState()
        {
            var state = (_allGeneratedOpportunities ?? new List<ResearchOpportunity>()).Select(opportunity =>
            {
                if (opportunity?.legacyMigrationCapture != null)
                    return new LegacyOpportunityMigrationDto(opportunity.legacyMigrationCapture.ToSavedState(opportunity), opportunity.loadID);
                try
                {
                    var spec = LegacyOpportunityAdapter.ToSpec(opportunity);
                    var category = IdentityFor<ResearchOpportunityCategoryDef>(opportunity.def.GetCategory(opportunity.relation));
                    return new LegacyOpportunityMigrationDto(new SavedOpportunityState(
                        spec.Key.Value, spec.Project, spec.Type, spec.Relation, spec.Requirement.Kind,
                        spec.Requirement.CanonicalSubject, category, opportunity.LegacyStoredProgress,
                        opportunity.StoredMaximumProgress, PreservedOpportunityStateKind.Ordinary,
                        spec.Requirement.AlternateSubjects, opportunity.loadID.ToString()), opportunity.loadID);
                }
                catch
                {
                    return new LegacyOpportunityMigrationDto(new SavedOpportunityState(
                        null, null, null, ResearchRelation.Direct, RequirementKind.None,
                        null, null,
                        opportunity?.LegacyStoredProgress ?? 0f,
                        opportunity?.StoredMaximumProgress ?? 0f,
                        PreservedOpportunityStateKind.Ordinary,
                        sourceId: opportunity?.loadID.ToString()), opportunity?.loadID);
                }
            }).ToArray();
            var budgets = (_categoryStores ?? new List<ResearchOpportunityCategoryTotalsStore>())
                .Where(store => store?.project != null && store.category != null && !float.IsNaN(store.researchPoints) && !float.IsInfinity(store.researchPoints) && store.researchPoints >= 0f)
                .Select(store => new SavedCategoryBudget(IdentityFor<ResearchProjectDef>(store.project), IdentityFor<ResearchOpportunityCategoryDef>(store.category), store.researchPoints))
                .ToArray();
            var projects = state.Select(item => item.State.Project).Where(item => item != null)
                .Concat((_projectsGenerated ?? new HashSet<ResearchProjectDef>()).Where(project => project != null).Select(IdentityFor<ResearchProjectDef>))
                .Distinct().ToArray();
            var active = _currentProject == null ? state.Select(item => item.State.Project).FirstOrDefault(item => item != null) : IdentityFor<ResearchProjectDef>(_currentProject);
            _opportunityService.RestoreLegacy(state!, budgets, active, projects!, changeTicker);
        }

        internal static DefIdentity IdentityFor<TDef>(TDef definition) where TDef : Def => new DefIdentity(typeof(TDef).Name, definition.defName);

        private static TDef ResolveDef<TDef>(DefIdentity identity) where TDef : Def
        {
            if (identity == null || !string.Equals(identity.DefType, typeof(TDef).Name, StringComparison.Ordinal))
                return null;
            return ResearchRuntimeServices.Current.AllDefsListForReading<TDef>()
                .FirstOrDefault(definition => string.Equals(definition.defName, identity.DefName, StringComparison.Ordinal));
        }
    }
}
