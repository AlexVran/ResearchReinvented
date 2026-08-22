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

		IEnumerable<OpportunityOverrideSnapshot?> OpportunityOverrides { get; }
	}

	[Flags]
	public enum ThingDefTraits
	{
		None = 0,
		Medicine = 1 << 0,
		Drug = 1 << 1,
		Ingestible = 1 << 2,
		RawFood = 1 << 3,
		Plant = 1 << 4,
		Pawn = 1 << 5,
		FleshPawn = 1 << 6,
		Corpse = 1 << 7,
		Haulable = 1 << 8,
		PlayerBuildable = 1 << 9,
		InstantBuild = 1 << 10,
		Flammable = 1 << 11
	}

	[Flags]
	public enum RecipeDefTraits
	{
		None = 0,
		Surgery = 1 << 0,
		Meaningful = 1 << 1,
		Blacklisted = 1 << 2
	}

	[Flags]
	public enum TerrainDefTraits
	{
		None = 0,
		Soil = 1 << 0,
		PlayerBuildable = 1 << 1
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
			IEnumerable<DefRequirementSnapshot?>? ingredients = null,
			RecipeDefTraits traits = RecipeDefTraits.None)
		{
			Identity = identity ?? throw new ArgumentNullException(nameof(identity));
			ResearchPrerequisites = Snapshot(researchPrerequisites);
			Products = Snapshot(products);
			Users = Snapshot(users);
			Ingredients = Snapshot(ingredients);
			Traits = traits;
		}

		public DefIdentity Identity { get; }

		public IReadOnlyList<DefIdentity?> ResearchPrerequisites { get; }

		public IReadOnlyList<DefCountSnapshot?> Products { get; }

		public IReadOnlyList<DefIdentity?> Users { get; }

		public IReadOnlyList<DefRequirementSnapshot?> Ingredients { get; }

		public RecipeDefTraits Traits { get; }

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
			IEnumerable<DefRequirementSnapshot?>? fuelRequirements = null,
			ThingDefTraits traits = ThingDefTraits.None,
			DefIdentity? corpseDefinition = null)
		{
			Identity = identity ?? throw new ArgumentNullException(nameof(identity));
			ResearchPrerequisites = Snapshot(researchPrerequisites);
			ConstructionCosts = Snapshot(constructionCosts);
			HarvestedProduct = harvestedProduct;
			FuelRequirements = Snapshot(fuelRequirements);
			Traits = traits;
			CorpseDefinition = corpseDefinition;
		}

		public DefIdentity Identity { get; }

		public IReadOnlyList<DefIdentity?> ResearchPrerequisites { get; }

		public IReadOnlyList<DefRequirementSnapshot?> ConstructionCosts { get; }

		public DefIdentity? HarvestedProduct { get; }

		public IReadOnlyList<DefRequirementSnapshot?> FuelRequirements { get; }

		public ThingDefTraits Traits { get; }

		public DefIdentity? CorpseDefinition { get; }

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
			IEnumerable<DefRequirementSnapshot?>? constructionCosts = null,
			TerrainDefTraits traits = TerrainDefTraits.None)
		{
			Identity = identity ?? throw new ArgumentNullException(nameof(identity));
			ResearchPrerequisites = new ReadOnlyCollection<DefIdentity?>(
				(researchPrerequisites ?? Enumerable.Empty<DefIdentity?>()).ToArray());
			ConstructionCosts = new ReadOnlyCollection<DefRequirementSnapshot?>(
				(constructionCosts ?? Enumerable.Empty<DefRequirementSnapshot?>()).ToArray());
			Traits = traits;
		}

		public DefIdentity Identity { get; }

		public IReadOnlyList<DefIdentity?> ResearchPrerequisites { get; }

		public IReadOnlyList<DefRequirementSnapshot?> ConstructionCosts { get; }

		public TerrainDefTraits Traits { get; }
	}

	public enum OpportunityOverrideAction
	{
		Force,
		Suppress,
		Replace,
		Reweight
	}

	public sealed class OpportunityOverrideSnapshot
	{
		public OpportunityOverrideSnapshot(
			DefIdentity sourceDef,
			OpportunityOverrideAction action,
			DefIdentity? project = null,
			DefIdentity? opportunityType = null,
			DefIdentity? subject = null,
			ResearchRelation? relation = null,
			DefIdentity? replacementOpportunityType = null,
			DefIdentity? replacementSubject = null,
			float importanceMultiplier = 1f)
		{
			SourceDef = sourceDef ?? throw new ArgumentNullException(nameof(sourceDef));
			Action = action;
			Project = project;
			OpportunityType = opportunityType;
			Subject = subject;
			Relation = relation;
			ReplacementOpportunityType = replacementOpportunityType;
			ReplacementSubject = replacementSubject;
			ImportanceMultiplier = importanceMultiplier;
		}

		public DefIdentity SourceDef { get; }
		public OpportunityOverrideAction Action { get; }
		public DefIdentity? Project { get; }
		public DefIdentity? OpportunityType { get; }
		public DefIdentity? Subject { get; }
		public ResearchRelation? Relation { get; }
		public DefIdentity? ReplacementOpportunityType { get; }
		public DefIdentity? ReplacementSubject { get; }
		public float ImportanceMultiplier { get; }
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
