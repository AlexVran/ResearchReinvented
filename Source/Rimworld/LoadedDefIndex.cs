#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using PeteTimesSix.ResearchReinvented.Defs;
using PeteTimesSix.ResearchReinvented.Domain;
using PeteTimesSix.ResearchReinvented.Domain.DefIndex;
using PeteTimesSix.ResearchReinvented.Opportunities;
using PeteTimesSix.ResearchReinvented.OpportunityComps;
using RimWorld;
using Verse;

namespace PeteTimesSix.ResearchReinvented.Rimworld
{
	internal sealed class LoadedDefSnapshotSource : IResearchDefSnapshotSource
	{
		internal LoadedDefSnapshotSource(IResearchRuntimeServices services)
		{
			if (services == null)
				throw new ArgumentNullException(nameof(services));

			var projects = services.AllDefsListForReading<ResearchProjectDef>().ToArray();
			var recipes = services.AllDefsListForReading<RecipeDef>().ToArray();
			var things = services.AllDefsListForReading<ThingDef>().ToArray();
			var terrains = services.AllDefsListForReading<TerrainDef>().ToArray();
			var specials = services.AllDefsListForReading<SpecialResearchOpportunityDef>().ToArray();
			var alternates = services.AllDefsListForReading<AlternateResearchSubjectsDef>().ToArray();
			var unlocks = BuildUnlocks(projects, recipes, things, terrains);
			var recipeUsers = BuildRecipeUsers(recipes, things);

			Projects = projects.Select(project => CaptureProject(project, ValuesFor(unlocks, project))).ToArray();
			Recipes = recipes.Select(recipe => CaptureRecipe(recipe, ValuesFor(recipeUsers, recipe))).ToArray();
			Things = things.Select(thing => CaptureThing(thing, things)).ToArray();
			Terrains = terrains.Select(CaptureTerrain).ToArray();
			SpecialOpportunities = specials.Select(CaptureSpecial).ToArray();
			AlternateLinks = alternates.SelectMany(CaptureAlternateLinks).ToArray();
		}

		public IEnumerable<ProjectDefSnapshot?> Projects { get; }

		public IEnumerable<RecipeDefSnapshot?> Recipes { get; }

		public IEnumerable<ThingDefSnapshot?> Things { get; }

		public IEnumerable<TerrainDefSnapshot?> Terrains { get; }

		public IEnumerable<SpecialOpportunitySnapshot?> SpecialOpportunities { get; }

		public IEnumerable<AlternateLinkSnapshot?> AlternateLinks { get; }

		private static ProjectDefSnapshot? CaptureProject(ResearchProjectDef? project, IEnumerable<Def> unlocks)
		{
			var identity = IdentityFor<ResearchProjectDef>(project);
			return identity == null
				? null
				: new ProjectDefSnapshot(
					identity,
					IdentitiesFor<ResearchProjectDef>(project!.prerequisites),
					IdentitiesFor<ResearchProjectDef>(project.hiddenPrerequisites),
					unlocks.Select(IdentityFor),
					IdentitiesFor<ThingDef>(project.requiredAnalyzed),
					IdentityFor<ThingDef>(project.Techprint));
		}

		private static RecipeDefSnapshot? CaptureRecipe(RecipeDef? recipe, IEnumerable<ThingDef> users)
		{
			var identity = IdentityFor<RecipeDef>(recipe);
			if (identity == null)
				return null;

			var prerequisites = Enumerable.Repeat(recipe!.researchPrerequisite, 1)
				.Concat(recipe.researchPrerequisites ?? Enumerable.Empty<ResearchProjectDef>());
			return new RecipeDefSnapshot(
				identity,
				IdentitiesFor<ResearchProjectDef>(prerequisites),
				(recipe.products ?? Enumerable.Empty<ThingDefCountClass>())
					.Select(product => new DefCountSnapshot(IdentityFor<ThingDef>(product?.thingDef), product?.count ?? 0f)),
				IdentitiesFor<ThingDef>(users),
				(recipe.ingredients ?? Enumerable.Empty<IngredientCount>())
					.Select(ingredient => CaptureRequirement(ingredient, recipe.fixedIngredientFilter)));
		}

		private static ThingDefSnapshot? CaptureThing(ThingDef? thing, IReadOnlyList<ThingDef> allThings)
		{
			var identity = IdentityFor<ThingDef>(thing);
			if (identity == null)
				return null;

			var costs = new List<DefRequirementSnapshot?>();
			costs.AddRange((thing!.CostList ?? Enumerable.Empty<ThingDefCountClass>())
				.Select(cost => DefRequirementSnapshot.Fixed(IdentityFor<ThingDef>(cost?.thingDef), cost?.count ?? 0f)));
			if (thing.CostStuffCount > 0)
			{
				costs.Add(DefRequirementSnapshot.Filter(
					AllowedStuffsFor(thing, allThings).Select(IdentityFor<ThingDef>),
					thing.CostStuffCount));
			}

			var fuelComp = thing.GetCompProperties<CompProperties_Refuelable>();
			var fuels = fuelComp?.fuelFilter == null
				? Enumerable.Empty<DefRequirementSnapshot?>()
				: new[] { DefRequirementSnapshot.Filter(fuelComp.fuelFilter.AllowedThingDefs.Select(IdentityFor<ThingDef>)) };
			return new ThingDefSnapshot(
				identity,
				IdentitiesFor<ResearchProjectDef>(thing.researchPrerequisites),
				costs,
				IdentityFor<ThingDef>(thing.plant?.harvestedThingDef),
				fuels);
		}

		private static TerrainDefSnapshot? CaptureTerrain(TerrainDef? terrain)
		{
			var identity = IdentityFor<TerrainDef>(terrain);
			return identity == null
				? null
				: new TerrainDefSnapshot(
					identity,
					IdentitiesFor<ResearchProjectDef>(terrain!.researchPrerequisites),
					(terrain.CostList ?? Enumerable.Empty<ThingDefCountClass>())
						.Select(cost => DefRequirementSnapshot.Fixed(IdentityFor<ThingDef>(cost?.thingDef), cost?.count ?? 0f)));
		}

		private static SpecialOpportunitySnapshot? CaptureSpecial(SpecialResearchOpportunityDef? special)
		{
			var identity = IdentityFor<SpecialResearchOpportunityDef>(special);
			if (identity == null)
				return null;

			var subjects = new List<SpecialSubjectSnapshot?>();
			subjects.AddRange((special!.things ?? new List<ThingDef>())
				.Select(thing => new SpecialSubjectSnapshot(IdentityFor<ThingDef>(thing), SpecialSubjectKind.Thing)));
			subjects.AddRange((special.terrains ?? new List<TerrainDef>())
				.Select(terrain => new SpecialSubjectSnapshot(IdentityFor<TerrainDef>(terrain), SpecialSubjectKind.Terrain)));
			subjects.AddRange((special.recipes ?? new List<RecipeDef>())
				.Select(recipe => new SpecialSubjectSnapshot(IdentityFor<RecipeDef>(recipe), SpecialSubjectKind.Recipe)));

			return new SpecialOpportunitySnapshot(
				identity,
				IdentityFor<ResearchProjectDef>(special.project),
				IdentityFor<ResearchOpportunityTypeDef>(special.opportunityType),
				subjects,
				special.relationOverride,
				special.forDirect,
				special.forAncestor,
				special.forDescendant,
				ToDomainMode(special.altsMode),
				special.importanceMultiplier,
				special.rare,
				special.freebie);
		}

		private static IEnumerable<AlternateLinkSnapshot?> CaptureAlternateLinks(AlternateResearchSubjectsDef? alternate)
		{
			var source = IdentityFor<AlternateResearchSubjectsDef>(alternate);
			if (source == null)
			{
				yield return null;
				yield break;
			}

			var inferred = alternate!.defName.StartsWith("RR_auto_", StringComparison.Ordinal);
			foreach (var link in Links(source, alternate.originals, alternate.alternatesEquivalent, AlternateSubjectMode.Equivalent, inferred))
				yield return link;
			foreach (var link in Links(source, alternate.originals, alternate.alternatesSimilar, AlternateSubjectMode.Similar, inferred))
				yield return link;
			foreach (var link in Links(source, alternate.originalTerrains, alternate.alternateEquivalentTerrains, AlternateSubjectMode.Equivalent, inferred))
				yield return link;
			foreach (var link in Links(source, alternate.originalTerrains, alternate.alternateSimilarTerrains, AlternateSubjectMode.Similar, inferred))
				yield return link;
			foreach (var link in Links(source, alternate.originalRecipes, alternate.alternateEquivalentRecipes, AlternateSubjectMode.Equivalent, inferred))
				yield return link;
			foreach (var link in Links(source, alternate.originalRecipes, alternate.alternateSimilarRecipes, AlternateSubjectMode.Similar, inferred))
				yield return link;
		}

		private static IEnumerable<AlternateLinkSnapshot> Links<TDef>(
			DefIdentity source,
			IEnumerable<TDef>? originals,
			IEnumerable<TDef>? alternates,
			AlternateSubjectMode mode,
			bool inferred) where TDef : Def
		{
			foreach (var original in originals ?? Enumerable.Empty<TDef>())
			{
				foreach (var alternate in alternates ?? Enumerable.Empty<TDef>())
					yield return new AlternateLinkSnapshot(source, IdentityFor<TDef>(original), IdentityFor<TDef>(alternate), mode, inferred);
			}
		}

		private static DefRequirementSnapshot? CaptureRequirement(IngredientCount? ingredient, ThingFilter? fixedFilter)
		{
			if (ingredient == null)
				return null;
			if (ingredient.IsFixedIngredient)
				return DefRequirementSnapshot.Fixed(IdentityFor<ThingDef>(ingredient.FixedIngredient), ingredient.GetBaseCount());

			var allowed = ingredient.filter?.AllowedThingDefs ?? Enumerable.Empty<ThingDef>();
			if (fixedFilter != null)
				allowed = allowed.Where(fixedFilter.Allows);
			return DefRequirementSnapshot.Filter(allowed.Select(IdentityFor<ThingDef>), ingredient.GetBaseCount());
		}

		private static IEnumerable<DefIdentity?> IdentitiesFor<TDef>(IEnumerable<TDef>? defs) where TDef : Def
		{
			return (defs ?? Enumerable.Empty<TDef>()).Select(IdentityFor<TDef>);
		}

		private static DefIdentity? IdentityFor(Def? def)
		{
			if (def == null || string.IsNullOrWhiteSpace(def.defName))
				return null;

			if (def is ThingDef)
				return new DefIdentity(nameof(ThingDef), def.defName);
			if (def is TerrainDef)
				return new DefIdentity(nameof(TerrainDef), def.defName);
			if (def is RecipeDef)
				return new DefIdentity(nameof(RecipeDef), def.defName);
			if (def is ResearchProjectDef)
				return new DefIdentity(nameof(ResearchProjectDef), def.defName);
			return new DefIdentity(def.GetType().Name, def.defName);
		}

		private static DefIdentity? IdentityFor<TDef>(TDef? def) where TDef : Def
		{
			return def == null || string.IsNullOrWhiteSpace(def.defName)
				? null
				: new DefIdentity(typeof(TDef).Name, def.defName);
		}

		private static AlternateSubjectMode ToDomainMode(AlternatesMode mode)
		{
			switch (mode)
			{
				case AlternatesMode.EQUIVALENT:
					return AlternateSubjectMode.Equivalent;
				case AlternatesMode.SIMILAR:
					return AlternateSubjectMode.Similar;
				default:
					return AlternateSubjectMode.None;
			}
		}

		private static Dictionary<ResearchProjectDef, List<Def>> BuildUnlocks(
			IEnumerable<ResearchProjectDef> projects,
			IEnumerable<RecipeDef> recipes,
			IEnumerable<ThingDef> things,
			IEnumerable<TerrainDef> terrains)
		{
			var result = projects.Where(project => project != null).ToDictionary(project => project, _ => new List<Def>());
			foreach (var recipe in recipes.Where(recipe => recipe != null))
			{
				AddUnlock(result, recipe.researchPrerequisite, recipe);
				foreach (var project in recipe.researchPrerequisites ?? Enumerable.Empty<ResearchProjectDef>())
					AddUnlock(result, project, recipe);
			}
			foreach (var thing in things.Where(thing => thing != null))
			{
				foreach (var project in thing.researchPrerequisites ?? Enumerable.Empty<ResearchProjectDef>())
					AddUnlock(result, project, thing);
			}
			foreach (var terrain in terrains.Where(terrain => terrain != null))
			{
				foreach (var project in terrain.researchPrerequisites ?? Enumerable.Empty<ResearchProjectDef>())
					AddUnlock(result, project, terrain);
			}
			return result;
		}

		private static Dictionary<RecipeDef, List<ThingDef>> BuildRecipeUsers(
			IEnumerable<RecipeDef> recipes,
			IEnumerable<ThingDef> things)
		{
			var result = recipes.Where(recipe => recipe != null).ToDictionary(recipe => recipe, _ => new List<ThingDef>());
			foreach (var recipe in result.Keys)
			{
				foreach (var user in recipe.recipeUsers ?? Enumerable.Empty<ThingDef>())
				{
					if (user != null && !result[recipe].Contains(user))
						result[recipe].Add(user);
				}
			}
			foreach (var thing in things.Where(thing => thing != null))
			{
				foreach (var recipe in thing.recipes ?? Enumerable.Empty<RecipeDef>())
				{
					if (recipe != null && result.TryGetValue(recipe, out var users) && !users.Contains(thing))
						users.Add(thing);
				}
			}
			return result;
		}

		private static IEnumerable<ThingDef> AllowedStuffsFor(ThingDef buildable, IEnumerable<ThingDef> things)
		{
			var categories = buildable.stuffCategories;
			if (categories == null || categories.Count == 0)
				return Enumerable.Empty<ThingDef>();
			return things.Where(candidate =>
				candidate?.stuffProps?.categories != null
				&& candidate.stuffProps.categories.Any(categories.Contains));
		}

		private static IEnumerable<TValue> ValuesFor<TKey, TValue>(
			IReadOnlyDictionary<TKey, List<TValue>> values,
			TKey? key) where TKey : class
		{
			return key != null && values.TryGetValue(key, out var result)
				? result
				: Enumerable.Empty<TValue>();
		}

		private static void AddUnlock(
			IReadOnlyDictionary<ResearchProjectDef, List<Def>> unlocks,
			ResearchProjectDef? project,
			Def definition)
		{
			if (project != null && unlocks.TryGetValue(project, out var definitions) && !definitions.Contains(definition))
				definitions.Add(definition);
		}
	}

	internal static class ResearchDefIndexSession
	{
		private static ResearchDefIndex? current;

		internal static ResearchDefIndex Current => current
			?? throw new InvalidOperationException("The immutable Research Reinvented Def index has not been initialized.");

		internal static void Initialize(IResearchRuntimeServices services)
		{
			if (current != null)
				return;

			var built = ResearchDefIndex.Build(new LoadedDefSnapshotSource(services));
			current = built;
			foreach (var cycle in built.PrerequisiteCycles)
				Log.Warning($"RR: immutable Def index found prerequisite cycle: {string.Join(" -> ", cycle.Projects.Select(project => project.DefName))}");
			foreach (var diagnostic in built.Diagnostics.Take(50))
				Log.Warning($"RR: immutable Def index ignored malformed loaded-Def data: {diagnostic.Detail}");
			if (built.Diagnostics.Count > 50)
				Log.Warning($"RR: immutable Def index suppressed {built.Diagnostics.Count - 50} additional malformed loaded-Def diagnostics.");
		}
	}
}
