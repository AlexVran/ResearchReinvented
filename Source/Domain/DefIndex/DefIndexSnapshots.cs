#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using PeteTimesSix.ResearchReinvented.Opportunities;

namespace PeteTimesSix.ResearchReinvented.Domain.DefIndex
{
	public interface IResearchDefSnapshotSource
	{
		IEnumerable<ProjectDefSnapshot?> Projects { get; }

		IEnumerable<RecipeDefSnapshot?> Recipes { get; }

		IEnumerable<ThingDefSnapshot?> Things { get; }

		IEnumerable<TerrainDefSnapshot?> Terrains { get; }

		IEnumerable<SpecialOpportunitySnapshot?> SpecialOpportunities { get; }

		IEnumerable<AlternateLinkSnapshot?> AlternateLinks { get; }
	}

	public sealed class ProjectDefSnapshot
	{
		public ProjectDefSnapshot(
			DefIdentity identity,
			IEnumerable<DefIdentity?>? prerequisites = null,
			IEnumerable<DefIdentity?>? hiddenPrerequisites = null,
			IEnumerable<DefIdentity?>? unlocks = null,
			IEnumerable<DefIdentity?>? requiredAnalyzed = null,
			DefIdentity? techprint = null)
		{
			Identity = identity ?? throw new ArgumentNullException(nameof(identity));
			Prerequisites = Snapshot(prerequisites);
			HiddenPrerequisites = Snapshot(hiddenPrerequisites);
			Unlocks = Snapshot(unlocks);
			RequiredAnalyzed = Snapshot(requiredAnalyzed);
			Techprint = techprint;
		}

		public DefIdentity Identity { get; }

		public IReadOnlyList<DefIdentity?> Prerequisites { get; }

		public IReadOnlyList<DefIdentity?> HiddenPrerequisites { get; }

		public IReadOnlyList<DefIdentity?> Unlocks { get; }

		public IReadOnlyList<DefIdentity?> RequiredAnalyzed { get; }

		public DefIdentity? Techprint { get; }

		private static IReadOnlyList<DefIdentity?> Snapshot(IEnumerable<DefIdentity?>? values)
		{
			return new ReadOnlyCollection<DefIdentity?>((values ?? Enumerable.Empty<DefIdentity?>()).ToArray());
		}
	}

	public sealed class RecipeDefSnapshot
	{
		public RecipeDefSnapshot(
			DefIdentity identity,
			IEnumerable<DefIdentity?>? researchPrerequisites = null,
			IEnumerable<DefCountSnapshot?>? products = null,
			IEnumerable<DefIdentity?>? users = null,
			IEnumerable<DefRequirementSnapshot?>? ingredients = null)
		{
			Identity = identity ?? throw new ArgumentNullException(nameof(identity));
			ResearchPrerequisites = Snapshot(researchPrerequisites);
			Products = Snapshot(products);
			Users = Snapshot(users);
			Ingredients = Snapshot(ingredients);
		}

		public DefIdentity Identity { get; }

		public IReadOnlyList<DefIdentity?> ResearchPrerequisites { get; }

		public IReadOnlyList<DefCountSnapshot?> Products { get; }

		public IReadOnlyList<DefIdentity?> Users { get; }

		public IReadOnlyList<DefRequirementSnapshot?> Ingredients { get; }

		private static IReadOnlyList<T?> Snapshot<T>(IEnumerable<T?>? values) where T : class
		{
			return new ReadOnlyCollection<T?>((values ?? Enumerable.Empty<T?>()).ToArray());
		}
	}

	public sealed class ThingDefSnapshot
	{
		public ThingDefSnapshot(
			DefIdentity identity,
			IEnumerable<DefIdentity?>? researchPrerequisites = null,
			IEnumerable<DefRequirementSnapshot?>? constructionCosts = null,
			DefIdentity? harvestedProduct = null,
			IEnumerable<DefRequirementSnapshot?>? fuelRequirements = null)
		{
			Identity = identity ?? throw new ArgumentNullException(nameof(identity));
			ResearchPrerequisites = Snapshot(researchPrerequisites);
			ConstructionCosts = Snapshot(constructionCosts);
			HarvestedProduct = harvestedProduct;
			FuelRequirements = Snapshot(fuelRequirements);
		}

		public DefIdentity Identity { get; }

		public IReadOnlyList<DefIdentity?> ResearchPrerequisites { get; }

		public IReadOnlyList<DefRequirementSnapshot?> ConstructionCosts { get; }

		public DefIdentity? HarvestedProduct { get; }

		public IReadOnlyList<DefRequirementSnapshot?> FuelRequirements { get; }

		private static IReadOnlyList<T?> Snapshot<T>(IEnumerable<T?>? values) where T : class
		{
			return new ReadOnlyCollection<T?>((values ?? Enumerable.Empty<T?>()).ToArray());
		}
	}

	public sealed class TerrainDefSnapshot
	{
		public TerrainDefSnapshot(
			DefIdentity identity,
			IEnumerable<DefIdentity?>? researchPrerequisites = null,
			IEnumerable<DefRequirementSnapshot?>? constructionCosts = null)
		{
			Identity = identity ?? throw new ArgumentNullException(nameof(identity));
			ResearchPrerequisites = new ReadOnlyCollection<DefIdentity?>(
				(researchPrerequisites ?? Enumerable.Empty<DefIdentity?>()).ToArray());
			ConstructionCosts = new ReadOnlyCollection<DefRequirementSnapshot?>(
				(constructionCosts ?? Enumerable.Empty<DefRequirementSnapshot?>()).ToArray());
		}

		public DefIdentity Identity { get; }

		public IReadOnlyList<DefIdentity?> ResearchPrerequisites { get; }

		public IReadOnlyList<DefRequirementSnapshot?> ConstructionCosts { get; }
	}

	public sealed class DefCountSnapshot
	{
		public DefCountSnapshot(DefIdentity? definition, float count)
		{
			Definition = definition;
			Count = count;
		}

		public DefIdentity? Definition { get; }

		public float Count { get; }
	}

	public sealed class DefRequirementSnapshot
	{
		private DefRequirementSnapshot(
			DefIdentity? fixedDefinition,
			IReadOnlyList<DefIdentity?> allowedDefinitions,
			float count,
			bool isFilter)
		{
			FixedDefinition = fixedDefinition;
			AllowedDefinitions = allowedDefinitions;
			Count = count;
			IsFilter = isFilter;
		}

		public DefIdentity? FixedDefinition { get; }

		public IReadOnlyList<DefIdentity?> AllowedDefinitions { get; }

		public float Count { get; }

		public bool IsFilter { get; }

		public static DefRequirementSnapshot Fixed(DefIdentity? definition, float count = 0f)
		{
			return new DefRequirementSnapshot(definition, Array.Empty<DefIdentity?>(), count, false);
		}

		public static DefRequirementSnapshot Filter(IEnumerable<DefIdentity?>? allowedDefinitions, float count = 0f)
		{
			return new DefRequirementSnapshot(
				null,
				new ReadOnlyCollection<DefIdentity?>((allowedDefinitions ?? Enumerable.Empty<DefIdentity?>()).ToArray()),
				count,
				true);
		}
	}

	public enum SpecialSubjectKind
	{
		Thing,
		Terrain,
		Recipe
	}

	public sealed class SpecialSubjectSnapshot
	{
		public SpecialSubjectSnapshot(DefIdentity? identity, SpecialSubjectKind kind)
		{
			Identity = identity;
			Kind = kind;
		}

		public DefIdentity? Identity { get; }

		public SpecialSubjectKind Kind { get; }
	}

	public sealed class SpecialOpportunitySnapshot
	{
		public SpecialOpportunitySnapshot(
			DefIdentity identity,
			DefIdentity? project,
			DefIdentity? opportunityType,
			IEnumerable<SpecialSubjectSnapshot?>? subjects,
			ResearchRelation? relationOverride = null,
			bool forDirect = true,
			bool forAncestor = false,
			bool forDescendant = false,
			AlternateSubjectMode alternateMode = AlternateSubjectMode.None,
			float importance = 1f,
			bool rare = false,
			bool freebie = true)
		{
			Identity = identity ?? throw new ArgumentNullException(nameof(identity));
			Project = project;
			OpportunityType = opportunityType;
			Subjects = new ReadOnlyCollection<SpecialSubjectSnapshot?>(
				(subjects ?? Enumerable.Empty<SpecialSubjectSnapshot?>()).ToArray());
			RelationOverride = relationOverride;
			ForDirect = forDirect;
			ForAncestor = forAncestor;
			ForDescendant = forDescendant;
			AlternateMode = alternateMode;
			Importance = importance;
			Rare = rare;
			Freebie = freebie;
		}

		public DefIdentity Identity { get; }

		public DefIdentity? Project { get; }

		public DefIdentity? OpportunityType { get; }

		public IReadOnlyList<SpecialSubjectSnapshot?> Subjects { get; }

		public ResearchRelation? RelationOverride { get; }

		public bool ForDirect { get; }

		public bool ForAncestor { get; }

		public bool ForDescendant { get; }

		public AlternateSubjectMode AlternateMode { get; }

		public float Importance { get; }

		public bool Rare { get; }

		public bool Freebie { get; }
	}

	public sealed class AlternateLinkSnapshot
	{
		public AlternateLinkSnapshot(
			DefIdentity sourceDef,
			DefIdentity? original,
			DefIdentity? alternate,
			AlternateSubjectMode mode,
			bool inferred)
		{
			SourceDef = sourceDef ?? throw new ArgumentNullException(nameof(sourceDef));
			Original = original;
			Alternate = alternate;
			Mode = mode;
			Inferred = inferred;
		}

		public DefIdentity SourceDef { get; }

		public DefIdentity? Original { get; }

		public DefIdentity? Alternate { get; }

		public AlternateSubjectMode Mode { get; }

		public bool Inferred { get; }
	}
}
