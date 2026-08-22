using PeteTimesSix.ResearchReinvented.Defs;
using PeteTimesSix.ResearchReinvented.Domain;
using PeteTimesSix.ResearchReinvented.Domain.Prototypes;
using PeteTimesSix.ResearchReinvented.Extensions;
using PeteTimesSix.ResearchReinvented.Opportunities;
using PeteTimesSix.ResearchReinvented.OpportunityComps;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace PeteTimesSix.ResearchReinvented.Managers
{
    /// <summary>
    /// RimWorld adapter for the per-game prototype authority. Semantic lifecycle
    /// is stored in PrototypeService; actual Thing and terrain references remain
    /// owned by each map's PrototypeTerrainGrid.
    /// </summary>
    public class PrototypeKeeper : GameComponent
    {
        public static PrototypeKeeper Instance => Current.Game.GetComponent<PrototypeKeeper>();

        private readonly PrototypeService prototypeService = new PrototypeService();
        internal IPrototypeService Service => prototypeService;

        [Unsaved(false)] private PrototypeSaveRootData prototypeSaveRoot;
        [Unsaved(false)] private HashSet<Thing> legacyPrototypes = new HashSet<Thing>();
        [Unsaved(false)] private ResearchProjectDef availabilityProject;
        [Unsaved(false)] private ResearchOpportunity[] prototypeOpportunities = Array.Empty<ResearchOpportunity>();
        [Unsaved(false)] private Dictionary<BuildableDef, ResearchOpportunity> buildableOpportunities = new Dictionary<BuildableDef, ResearchOpportunity>();
        [Unsaved(false)] private Dictionary<RecipeDef, ResearchOpportunity> recipeOpportunities = new Dictionary<RecipeDef, ResearchOpportunity>();
        [Unsaved(false)] private Dictionary<ThingDef, ResearchOpportunity> thingOpportunities = new Dictionary<ThingDef, ResearchOpportunity>();

        public PrototypeKeeper(Game game) { }

        public IReadOnlyCollection<Thing> Prototypes => (Find.Maps ?? new List<Map>())
            .SelectMany(map => GetMapPrototypeTerrainGrid(map)?.Prototypes ?? Array.Empty<Thing>())
            .Where(thing => thing != null && !thing.Destroyed)
            .Distinct().ToArray();

        public IReadOnlyList<ResearchOpportunity> PrototypeOpportunities
        {
            get
            {
                EnsureAvailabilityIndex();
                return prototypeOpportunities;
            }
        }

        public IReadOnlyList<RecipeDef> ExperimentalSurgeryRecipesFor(Pawn patient)
        {
            var project = Find.ResearchManager?.GetProject();
            if (patient == null || project == null) return Array.Empty<RecipeDef>();
            return ResearchOpportunityManager.Instance.OpportunityService.SpecificationsFor(ProjectIdentity(project))
                .Where(specification => specification.Spec.Type.DefName == "PrototypeSurgery"
                    && specification.Spec.Requirement.Kind == RequirementKind.Recipe)
                .SelectMany(specification => new[] { specification.Spec.Requirement.CanonicalSubject }
                    .Concat(specification.Spec.Requirement.AlternateSubjects))
                .Where(identity => identity.DefType == "RecipeDef")
                .Select(identity => DefDatabase<RecipeDef>.GetNamedSilentFail(identity.DefName))
                .Where(recipe => recipe != null && patient.def.AllRecipes.Contains(recipe))
                .Distinct().OrderBy(recipe => recipe.defName, StringComparer.Ordinal).ToArray();
        }

        public PrototypeTerrainGrid GetMapPrototypeTerrainGrid(Map map) => map?.GetComponent<PrototypeTerrainGrid>();

        public void InvalidateAvailability()
        {
            availabilityProject = null;
            prototypeOpportunities = Array.Empty<ResearchOpportunity>();
            buildableOpportunities.Clear();
            recipeOpportunities.Clear();
            thingOpportunities.Clear();
        }

        public ResearchOpportunity GetPrototypeOpportunity(Def definition)
        {
            EnsureAvailabilityIndex();
            if (definition is RecipeDef recipe && recipeOpportunities.TryGetValue(recipe, out var recipeOpportunity)) return recipeOpportunity;
            if (definition is ThingDef thing && thingOpportunities.TryGetValue(thing, out var thingOpportunity)) return thingOpportunity;
            if (definition is BuildableDef buildable && buildableOpportunities.TryGetValue(buildable, out var buildableOpportunity)) return buildableOpportunity;
            return null;
        }

        public bool IsPrototype(Thing thing)
        {
            var map = thing?.MapHeld;
            return map != null && (GetMapPrototypeTerrainGrid(map)?.IsPrototype(thing) ?? false);
        }

        public PrototypeArtifactKey MarkAsPrototype(Thing thing) => RegisterThing(thing, InferKind(thing), PrototypeLifecycleState.Active);

        public PrototypeArtifactKey RegisterThing(Thing thing, PrototypeArtifactKind kind, PrototypeLifecycleState state, Def opportunitySubject = null, Map mapOverride = null)
        {
            TryRegisterThing(thing, kind, state, out var key, opportunitySubject, mapOverride);
            return key;
        }

        public bool TryRegisterThing(Thing thing, PrototypeArtifactKind kind, PrototypeLifecycleState state, out PrototypeArtifactKey key, Def opportunitySubject = null, Map mapOverride = null)
        {
            key = null;
            var project = Find.ResearchManager?.GetProject();
            var map = thing?.MapHeld ?? mapOverride;
            if (thing == null || map == null || project == null || !prototypeService.Enabled)
                return false;
            GetMapPrototypeTerrainGrid(map)?.MarkAsPrototype(thing);
            key = ThingKey(thing, project, kind, map);
            var subject = opportunitySubject ?? SubjectFor(thing);
            var opportunity = GetPrototypeOpportunity(subject)?.AuthoritativeKey;
            return prototypeService.Register(new PrototypeRecord(key, ProjectIdentity(project), kind, state, opportunity, subject?.defName));
        }

        public bool TransitionThing(PrototypeArtifactKey fromKey, Thing from, Thing to, PrototypeArtifactKind kind, PrototypeLifecycleState state, Map mapOverride = null)
        {
            if (fromKey == null || to == null)
                return false;
            var source = prototypeService.Records.FirstOrDefault(item => item.Key.Equals(fromKey));
            var map = to.MapHeld ?? mapOverride;
            if (source == null || map == null)
                return false;
            GetMapPrototypeTerrainGrid(from?.MapHeld)?.UnmarkAsPrototype(from);
            GetMapPrototypeTerrainGrid(map)?.MarkAsPrototype(to);
            var project = ResolveProject(source.Project);
            if (project == null) return false;
            var toKey = ThingKey(to, project, kind, map);
            return prototypeService.Transition(fromKey, toKey, kind, state);
        }

        public bool FinishThing(PrototypeArtifactKey key, Thing thing, PrototypeLifecycleState state)
        {
            if (thing != null)
                GetMapPrototypeTerrainGrid(thing.MapHeld)?.UnmarkAsPrototype(thing);
            return key != null && prototypeService.Finish(key, state);
        }

        public void UnmarkAsPrototype(Thing thing)
        {
            if (thing == null) return;
            GetMapPrototypeTerrainGrid(thing.MapHeld)?.UnmarkAsPrototype(thing);
            var project = Find.ResearchManager?.GetProject();
            if (project == null || thing.MapHeld == null) return;
            foreach (PrototypeArtifactKind kind in Enum.GetValues(typeof(PrototypeArtifactKind)))
                prototypeService.Finish(ThingKey(thing, project, kind, thing.MapHeld), PrototypeLifecycleState.Cancelled);
        }

        public PrototypeArtifactKey RegisterBill(Bill bill, IBillGiver giver)
        {
            var project = Find.ResearchManager?.GetProject();
            var map = (giver as Thing)?.MapHeld;
            if (bill == null || map == null || project == null || !bill.recipe.IsAvailableOnlyForPrototyping(true)) return null;
            var kind = bill is Bill_Medical ? PrototypeArtifactKind.SurgeryBill : PrototypeArtifactKind.Bill;
            var key = PrototypeArtifactKey.Create(ProjectIdentity(project), MapKey(map), kind, bill.GetUniqueLoadID());
            prototypeService.Register(new PrototypeRecord(key, ProjectIdentity(project), kind, PrototypeLifecycleState.Active, GetPrototypeOpportunity(bill.recipe)?.AuthoritativeKey, bill.recipe.defName));
            return key;
        }

        public bool FinishBill(Bill bill, IBillGiver giver, PrototypeLifecycleState state)
        {
            var project = Find.ResearchManager?.GetProject();
            var map = (giver as Thing)?.MapHeld;
            if (bill == null || map == null || project == null) return false;
            var kind = bill is Bill_Medical ? PrototypeArtifactKind.SurgeryBill : PrototypeArtifactKind.Bill;
            return prototypeService.Finish(PrototypeArtifactKey.Create(ProjectIdentity(project), MapKey(map), kind, bill.GetUniqueLoadID()), state);
        }

        public bool IsTerrainPrototype(IntVec3 position, Map map) => GetMapPrototypeTerrainGrid(map)?.IsTerrainPrototype(position) ?? false;
        public bool IsFoundationTerrainPrototype(IntVec3 position, Map map) => GetMapPrototypeTerrainGrid(map)?.IsFoundationTerrainPrototype(position) ?? false;

        public void MarkTerrainAsPrototype(IntVec3 position, Map map, TerrainDef terrain)
        {
            GetMapPrototypeTerrainGrid(map)?.MarkTerrainAsPrototype(position, terrain);
            RegisterTerrain(position, map, terrain, PrototypeArtifactKind.Terrain);
        }

        public void UnmarkTerrainAsPrototype(IntVec3 position, Map map) => GetMapPrototypeTerrainGrid(map)?.UnmarkTerrainAsPrototype(position);

        public void MarkFoundationTerrainAsPrototype(IntVec3 position, Map map, TerrainDef terrain)
        {
            GetMapPrototypeTerrainGrid(map)?.MarkFoundationTerrainAsPrototype(position, terrain);
            RegisterTerrain(position, map, terrain, PrototypeArtifactKind.FoundationTerrain);
        }

        public void UnmarkFoundationTerrainAsPrototype(IntVec3 position, Map map) => GetMapPrototypeTerrainGrid(map)?.UnmarkFoundationTerrainAsPrototype(position);

        public void DebugDrawOnMap()
        {
            if (ResearchReinvented_Debug.drawPrototypeGrid)
                GetMapPrototypeTerrainGrid(Find.CurrentMap)?.DebugDrawOnMap();
        }

        public bool DisableFeature(string feature, string reason)
        {
            var first = prototypeService.DisableFeature(feature, reason);
            if (first) Log.Warning($"RR prototypes: {reason} Ordinary research remains enabled.");
            return first;
        }

        public bool IsFeatureEnabled(string feature) => prototypeService.IsFeatureEnabled(feature);

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            if (prototypeService.Records.Count != 0 || prototypeService.IsReadOnly) return;
            foreach (var thing in Prototypes.ToArray())
                RegisterThing(thing, InferKind(thing), thing is Blueprint || thing is Frame || thing is UnfinishedThing
                    ? PrototypeLifecycleState.Active : PrototypeLifecycleState.Completed);
        }

        public override void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                if (prototypeService.IsReadOnly)
                    throw new InvalidOperationException("Research Reinvented cannot overwrite prototype state written by a newer schema.");
                prototypeSaveRoot = new PrototypeSaveRootData(prototypeService.CreateSnapshot());
            }
            Scribe_Deep.Look(ref prototypeSaveRoot, "prototypeState");
            if (prototypeSaveRoot == null && Scribe.mode != LoadSaveMode.Saving)
                Scribe_Collections.Look(ref legacyPrototypes, "_prototypes", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (prototypeSaveRoot != null)
                    prototypeService.Restore(prototypeSaveRoot.ToDomain());
                else
                    MigrateLegacyReferences();
                legacyPrototypes = new HashSet<Thing>();
            }
        }

        public void CancelPrototypes(ResearchProjectDef previousProject, ResearchProjectDef currentProject)
        {
            if (ResearchReinventedMod.Settings.disablePrototypeBillCancellation || previousProject == null || previousProject == currentProject)
                return;

            var cancelled = prototypeService.CancelProject(ProjectIdentity(previousProject));
            if (cancelled.Count == 0) return;
            var keys = new HashSet<string>(cancelled.Select(item => item.Key.Value), StringComparer.Ordinal);
            foreach (var map in Find.Maps)
            {
                var grid = GetMapPrototypeTerrainGrid(map);
                foreach (var thing in (grid?.Prototypes ?? Array.Empty<Thing>()).ToArray())
                {
                    var key = ThingKey(thing, previousProject, InferKind(thing), map);
                    if (!keys.Contains(key.Value)) continue;
                    grid.UnmarkAsPrototype(thing);
                    if (!thing.Destroyed) thing.Destroy(DestroyMode.Cancel);
                }
                foreach (var giver in map.listerThings.ThingsInGroup(ThingRequestGroup.PotentialBillGiver).OfType<IBillGiver>())
                {
                    foreach (var bill in giver.BillStack.Bills.ToArray())
                    {
                        var kind = bill is Bill_Medical ? PrototypeArtifactKind.SurgeryBill : PrototypeArtifactKind.Bill;
                        var key = PrototypeArtifactKey.Create(ProjectIdentity(previousProject), MapKey(map), kind, bill.GetUniqueLoadID());
                        if (keys.Contains(key.Value)) giver.BillStack.Delete(bill);
                    }
                }
            }
        }

        private void RegisterTerrain(IntVec3 position, Map map, TerrainDef terrain, PrototypeArtifactKind kind)
        {
            var project = Find.ResearchManager?.GetProject();
            if (project == null || map == null || terrain == null) return;
            var key = PrototypeArtifactKey.Create(ProjectIdentity(project), MapKey(map), kind, $"{position.x},{position.z}");
            prototypeService.Register(new PrototypeRecord(key, ProjectIdentity(project), kind, PrototypeLifecycleState.Completed, GetPrototypeOpportunity(terrain)?.AuthoritativeKey, terrain.defName));
        }

        private void MigrateLegacyReferences()
        {
            var project = Find.ResearchManager?.GetProject();
            foreach (var thing in (legacyPrototypes ?? new HashSet<Thing>()).Where(thing => thing != null && !thing.Destroyed))
            {
                GetMapPrototypeTerrainGrid(thing.MapHeld)?.MarkAsPrototype(thing);
                if (project != null && thing.MapHeld != null)
                    RegisterThing(thing, InferKind(thing), PrototypeLifecycleState.Active);
            }
        }

        private void EnsureAvailabilityIndex()
        {
            var project = Find.ResearchManager?.GetProject();
            if (availabilityProject == project) return;
            availabilityProject = project;
            prototypeOpportunities = project == null
                ? Array.Empty<ResearchOpportunity>()
                : ResearchOpportunityManager.Instance.GetFilteredOpportunities(null, HandlingMode.Special_Prototype).Where(item => item.project == project).ToArray();
            buildableOpportunities.Clear();
            recipeOpportunities.Clear();
            thingOpportunities.Clear();
            foreach (var opportunity in prototypeOpportunities)
            {
                if (opportunity.requirement is ROComp_RequiresThing thingRequirement)
                    foreach (var thing in thingRequirement.AllThings.Where(item => item != null))
                    {
                        thingOpportunities[thing] = opportunity;
                        buildableOpportunities[thing] = opportunity;
                    }
                else if (opportunity.requirement is ROComp_RequiresTerrain terrainRequirement)
                    foreach (var terrain in terrainRequirement.AllTerrains.Where(item => item != null)) buildableOpportunities[terrain] = opportunity;
                else if (opportunity.requirement is ROComp_RequiresRecipe recipeRequirement)
                    foreach (var recipe in recipeRequirement.AllRecipes.Where(item => item != null)) recipeOpportunities[recipe] = opportunity;
            }
        }

        private static Def SubjectFor(Thing thing) => thing is Frame frame ? frame.def.entityDefToBuild : thing.def;
        private static PrototypeArtifactKind InferKind(Thing thing) => thing is Blueprint ? PrototypeArtifactKind.Blueprint
            : thing is Frame ? PrototypeArtifactKind.Frame
            : thing is UnfinishedThing ? PrototypeArtifactKind.UnfinishedItem
            : PrototypeArtifactKind.CompletedProduct;
        private static PrototypeArtifactKey ThingKey(Thing thing, ResearchProjectDef project, PrototypeArtifactKind kind, Map map) =>
            PrototypeArtifactKey.Create(ProjectIdentity(project), MapKey(map), kind, thing.GetUniqueLoadID());
        private static string MapKey(Map map) => map.uniqueID.ToString();
        private static DefIdentity ProjectIdentity(ResearchProjectDef project) => new DefIdentity("ResearchProjectDef", project.defName);
        private static ResearchProjectDef ResolveProject(DefIdentity identity) => DefDatabase<ResearchProjectDef>.GetNamedSilentFail(identity.DefName);
    }
}
