using PeteTimesSix.ResearchReinvented.Defs;
using PeteTimesSix.ResearchReinvented.Domain;
using PeteTimesSix.ResearchReinvented.Domain.Presentation;
using PeteTimesSix.ResearchReinvented.Managers;
using PeteTimesSix.ResearchReinvented.Opportunities;
using PeteTimesSix.ResearchReinvented.OpportunityComps;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEngine;
using Verse;

namespace PeteTimesSix.ResearchReinvented.Rimworld.UI
{
    internal enum OpportunityIconKind
    {
        Science,
        Generic,
        Thing,
        Recipe,
        Terrain,
        Faction,
    }

    internal sealed class OpportunityPresentationViewModel
    {
        internal OpportunityPresentationViewModel(
            OpportunityReadModel state,
            string header,
            string requirementLabel,
            string tooltip,
            OpportunityAvailability availability,
            string dynamicAvailabilityReason,
            string developerDetails,
            bool infiniteOverflow,
            IEnumerable<Texture2D> handlingIcons,
            OpportunityIconKind iconKind,
            ThingDef thing,
            RecipeDef recipe,
            TerrainDef terrain,
            Faction faction)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
            Header = header ?? string.Empty;
            RequirementLabel = requirementLabel ?? string.Empty;
            Tooltip = tooltip ?? string.Empty;
            Availability = availability;
            DynamicAvailabilityReason = dynamicAvailabilityReason ?? string.Empty;
            DeveloperDetails = developerDetails ?? string.Empty;
            InfiniteOverflow = infiniteOverflow;
            HandlingIcons = new ReadOnlyCollection<Texture2D>((handlingIcons ?? Array.Empty<Texture2D>()).Where(icon => icon != null).ToArray());
            IconKind = iconKind;
            Thing = thing;
            Recipe = recipe;
            Terrain = terrain;
            Faction = faction;
        }

        internal OpportunityReadModel State { get; }
        internal OpportunityKey Key => State.Key;
        internal string Header { get; }
        internal string RequirementLabel { get; }
        internal string Tooltip { get; }
        internal OpportunityAvailability Availability { get; }
        internal string DynamicAvailabilityReason { get; }
        internal string DeveloperDetails { get; }
        internal bool InfiniteOverflow { get; }
        internal IReadOnlyList<Texture2D> HandlingIcons { get; }
        internal OpportunityIconKind IconKind { get; }
        internal ThingDef Thing { get; }
        internal RecipeDef Recipe { get; }
        internal TerrainDef Terrain { get; }
        internal Faction Faction { get; }
    }

    internal sealed class OpportunityCategoryPresentationViewModel
    {
        internal OpportunityCategoryPresentationViewModel(
            OpportunityCategoryReadModel state,
            ResearchOpportunityCategoryDef definition,
            IEnumerable<OpportunityPresentationViewModel> opportunities)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
            Definition = definition;
            Label = definition?.LabelCap.ToString() ?? state.Category.DefName;
            Description = definition?.description ?? state.Category.CanonicalValue;
            Color = definition?.color ?? Color.gray;
            Priority = definition?.priority ?? int.MinValue;
            Enabled = definition?.Settings.enabled ?? true;
            InfiniteOverflow = definition?.Settings.infiniteOverflow ?? false;
            Opportunities = new ReadOnlyCollection<OpportunityPresentationViewModel>((opportunities ?? Array.Empty<OpportunityPresentationViewModel>())
                .OrderByDescending(item => item.State.MaximumProgress)
                .ThenBy(item => item.Key)
                .ToArray());
        }

        internal OpportunityCategoryReadModel State { get; }
        internal DefIdentity Key => State.Category;
        internal ResearchOpportunityCategoryDef Definition { get; }
        internal string Label { get; }
        internal string Description { get; }
        internal Color Color { get; }
        internal int Priority { get; }
        internal bool Enabled { get; }
        internal bool InfiniteOverflow { get; }
        internal IReadOnlyList<OpportunityPresentationViewModel> Opportunities { get; }
    }

    internal sealed class OpportunityProjectPresentationViewModel
    {
        internal OpportunityProjectPresentationViewModel(ResearchProjectDef project, IEnumerable<OpportunityCategoryPresentationViewModel> categories)
        {
            Project = project;
            Categories = new ReadOnlyCollection<OpportunityCategoryPresentationViewModel>((categories ?? Array.Empty<OpportunityCategoryPresentationViewModel>())
                .OrderByDescending(item => item.Priority)
                .ThenBy(item => item.Key)
                .ToArray());
        }

        internal ResearchProjectDef Project { get; }
        internal IReadOnlyList<OpportunityCategoryPresentationViewModel> Categories { get; }
    }

    internal static class OpportunityPresentationFactory
    {
        internal static OpportunityProjectPresentationViewModel Create(ResearchOpportunityManager manager)
        {
            if (manager == null) throw new ArgumentNullException(nameof(manager));
            manager.CheckForRegeneration();
            var project = manager.CurrentProject;
            if (project == null)
                return new OpportunityProjectPresentationViewModel(null, Array.Empty<OpportunityCategoryPresentationViewModel>());

            var projectIdentity = ResearchOpportunityManager.IdentityFor<ResearchProjectDef>(project);
            var state = manager.OpportunityService.ReadModelFor(projectIdentity);
            var projections = manager.AllGeneratedOpportunities
                .Where(item => item?.project == project && item.AuthoritativeKey != null)
                .GroupBy(item => item.AuthoritativeKey)
                .ToDictionary(group => group.Key, group => group.First());
            var categories = state.Categories.Select(categoryState =>
            {
                var categoryDef = LoadedPresentationDefCatalog.Category(categoryState.Category);
                var opportunities = categoryState.Opportunities.Select(opportunityState =>
                {
                    projections.TryGetValue(opportunityState.Key, out var projection);
                    var typeDef = LoadedPresentationDefCatalog.Type(opportunityState.Type);
                    return CreateOpportunity(manager, project, categoryDef, typeDef, opportunityState, projection, categoryState.Progress, categoryState.Budget, categoryState.HasBudget);
                });
                return new OpportunityCategoryPresentationViewModel(categoryState, categoryDef, opportunities);
            });
            return new OpportunityProjectPresentationViewModel(project, categories);
        }

        private static OpportunityPresentationViewModel CreateOpportunity(
            ResearchOpportunityManager manager,
            ResearchProjectDef project,
            ResearchOpportunityCategoryDef category,
            ResearchOpportunityTypeDef type,
            OpportunityReadModel state,
            ResearchOpportunity projection,
            float categoryProgress,
            float categoryBudget,
            bool hasCategoryBudget)
        {
            var requirement = projection?.requirement;
            var requirementLabel = requirement?.ShortDesc.CapitalizeFirst() ?? state.Requirement.CanonicalSubject.DefName.CapitalizeFirst();
            var subject = requirement?.Subject.ToString() ?? state.Requirement.CanonicalSubject.DefName;
            var header = type?.GetHeaderCap(state.Relation).ToString() ?? state.Type.DefName;
            var tooltip = type == null ? header + ": " + subject : type.GetShortDescCap(state.Relation).Formatted(subject).ToString();
            var availability = Availability(project, category, state, categoryProgress, categoryBudget, hasCategoryBudget, out var dynamicReason);
            var handlerReasons = projection == null ? Array.Empty<string>() : manager.Execution.UnavailabilityReasonsFor(projection);
            var developerDetails = DeveloperDetails(state, availability, dynamicReason, projection == null, handlerReasons);
            var iconKind = OpportunityIconKind.Science;
            ThingDef thing = null;
            RecipeDef recipe = null;
            TerrainDef terrain = null;
            Faction faction = null;
            if (requirement is ROComp_RequiresFactionlessPawn)
                iconKind = OpportunityIconKind.Generic;
            else if (requirement is ROComp_RequiresFaction factionRequirement)
            {
                iconKind = OpportunityIconKind.Faction;
                faction = factionRequirement.faction;
            }
            else if (requirement is ROComp_RequiresRecipe recipeRequirement)
            {
                iconKind = OpportunityIconKind.Recipe;
                recipe = recipeRequirement.ShownCycledRecipe;
            }
            else if (requirement is ROComp_RequiresThing thingRequirement)
            {
                iconKind = OpportunityIconKind.Thing;
                thing = thingRequirement.ShownCycledThing;
            }
            else if (requirement is ROComp_RequiresTerrain terrainRequirement)
            {
                iconKind = OpportunityIconKind.Terrain;
                terrain = terrainRequirement.ShownCycledTerrain;
            }
            else if (requirement is ROComp_RequiresSchematicWithProject)
            {
                iconKind = OpportunityIconKind.Thing;
                thing = ThingDefOf.Schematic;
            }

            return new OpportunityPresentationViewModel(
                state, header, requirementLabel, tooltip, availability, dynamicReason, developerDetails,
                category?.Settings.infiniteOverflow ?? false,
                type != null ? type.Icons : Array.Empty<Texture2D>(), iconKind, thing, recipe, terrain, faction);
        }

        private static OpportunityAvailability Availability(
            ResearchProjectDef project,
            ResearchOpportunityCategoryDef category,
            OpportunityReadModel state,
            float categoryProgress,
            float categoryBudget,
            bool hasCategoryBudget,
            out string detail)
        {
            if (state.IsFinished)
            {
                detail = "The authoritative opportunity progress reached its maximum.";
                return OpportunityAvailability.Finished;
            }
            if (category == null)
            {
                detail = "The saved category Def is not loaded; state remains visible but cannot execute.";
                return OpportunityAvailability.UnavailableReasonUnknown;
            }
            var settings = category.Settings;
            if (!settings.enabled)
            {
                detail = $"Category {category.defName} is disabled by settings.";
                return OpportunityAvailability.Disabled;
            }
            if (project.ProgressPercent < settings.availableAtOverallProgress.min)
            {
                detail = $"Project progress {project.ProgressPercent:P1} is below {settings.availableAtOverallProgress.min:P1}.";
                return OpportunityAvailability.ResearchTooLow;
            }
            if (project.ProgressPercent > settings.availableAtOverallProgress.max)
            {
                detail = $"Project progress {project.ProgressPercent:P1} is above {settings.availableAtOverallProgress.max:P1}.";
                return OpportunityAvailability.ResearchTooHigh;
            }
            if (!settings.infiniteOverflow && categoryProgress >= categoryBudget)
            {
                if (!hasCategoryBudget)
                {
                    detail = "No authoritative category budget is available.";
                    return OpportunityAvailability.UnavailableReasonUnknown;
                }
                detail = $"Authoritative category progress {categoryProgress:0.###} reached budget {categoryBudget:0.###}.";
                return OpportunityAvailability.CategoryFinished;
            }
            detail = "Current project progress and category settings permit this opportunity.";
            return OpportunityAvailability.Available;
        }

        private static string DeveloperDetails(OpportunityReadModel state, OpportunityAvailability availability, string dynamicReason, bool missingProjection, IReadOnlyList<string> handlerReasons)
        {
            var reasons = state.Reasons.Count == 0
                ? "  (none recorded)"
                : string.Join("\n", state.Reasons.Select(reason => $"  {reason.Kind} / {reason.Source} / {reason.SourceDef.CanonicalValue}: {reason.Detail ?? "-"}"));
            var handlers = handlerReasons.Count == 0 ? "none" : string.Join(" | ", handlerReasons);
            return $"Key: {state.Key.Value}\nRelation: {state.Relation}; importance: {state.Importance:0.###}; rare: {state.Rare}; freebie: {state.Freebie}; state: {state.StateKind}\nAvailability: {availability} — {dynamicReason}\nRuntime projection: {(missingProjection ? "missing (state retained)" : "available")}\nUnavailable activity handlers: {handlers}\nGeneration reasons:\n{reasons}";
        }
    }
}
