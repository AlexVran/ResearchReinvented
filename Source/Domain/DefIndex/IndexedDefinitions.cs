#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using PeteTimesSix.ResearchReinvented.Opportunities;

namespace PeteTimesSix.ResearchReinvented.Domain.DefIndex
{
	public enum PrerequisiteKind
	{
		Direct,
		Hidden
	}

	public sealed class PrerequisiteEdge
	{
		public PrerequisiteEdge(DefIdentity project, DefIdentity prerequisite, PrerequisiteKind kind)
		{
			Project = project ?? throw new ArgumentNullException(nameof(project));
			Prerequisite = prerequisite ?? throw new ArgumentNullException(nameof(prerequisite));
			Kind = kind;
		}

		public DefIdentity Project { get; }

		public DefIdentity Prerequisite { get; }

		public PrerequisiteKind Kind { get; }
	}

	public sealed class CompactDefFilter
	{
		private readonly IReadOnlyList<ulong> words;

		internal CompactDefFilter(string key, int allowedDefinitionCount, ulong[] words)
		{
			Key = key ?? throw new ArgumentNullException(nameof(key));
			AllowedDefinitionCount = allowedDefinitionCount;
			this.words = new ReadOnlyCollection<ulong>((ulong[])words.Clone());
			EvidenceSubject = new DefIdentity("ThingFilter", key);
		}

		public string Key { get; }

		public int AllowedDefinitionCount { get; }

		public IReadOnlyList<ulong> EncodedWords => words;

		public DefIdentity EvidenceSubject { get; }

		internal bool AllowsOrdinal(int ordinal)
		{
			if (ordinal < 0)
				return false;

			var word = ordinal / 64;
			return word < words.Count && (words[word] & (1UL << (ordinal % 64))) != 0;
		}
	}

	public sealed class IndexedRequirement
	{
		internal IndexedRequirement(DefIdentity? fixedDefinition, CompactDefFilter? filter, float count)
		{
			FixedDefinition = fixedDefinition;
			Filter = filter;
			Count = count;
		}

		public DefIdentity? FixedDefinition { get; }

		public CompactDefFilter? Filter { get; }

		public float Count { get; }

		public DefIdentity EvidenceSubject => FixedDefinition ?? Filter!.EvidenceSubject;
	}

	public sealed class IndexedDefCount
	{
		internal IndexedDefCount(DefIdentity definition, float count)
		{
			Definition = definition;
			Count = count;
		}

		public DefIdentity Definition { get; }

		public float Count { get; }
	}

	public sealed class IndexedProject
	{
		internal IndexedProject(
			DefIdentity identity,
			IReadOnlyList<PrerequisiteEdge> prerequisites,
			IReadOnlyList<DefIdentity> unlocks,
			IReadOnlyList<DefIdentity> requiredAnalyzed,
			DefIdentity? techprint)
		{
			Identity = identity;
			Prerequisites = prerequisites;
			Unlocks = unlocks;
			RequiredAnalyzed = requiredAnalyzed;
			Techprint = techprint;
		}

		public DefIdentity Identity { get; }

		public IReadOnlyList<PrerequisiteEdge> Prerequisites { get; }

		public IReadOnlyList<DefIdentity> Unlocks { get; }

		public IReadOnlyList<DefIdentity> RequiredAnalyzed { get; }

		public DefIdentity? Techprint { get; }
	}

	public sealed class IndexedRecipe
	{
		internal IndexedRecipe(
			DefIdentity identity,
			IReadOnlyList<DefIdentity> researchPrerequisites,
			IReadOnlyList<IndexedDefCount> products,
			IReadOnlyList<DefIdentity> users,
			IReadOnlyList<IndexedRequirement> ingredients,
			RecipeDefTraits traits)
		{
			Identity = identity;
			ResearchPrerequisites = researchPrerequisites;
			Products = products;
			Users = users;
			Ingredients = ingredients;
			Traits = traits;
		}

		public DefIdentity Identity { get; }

		public IReadOnlyList<DefIdentity> ResearchPrerequisites { get; }

		public IReadOnlyList<IndexedDefCount> Products { get; }

		public IReadOnlyList<DefIdentity> Users { get; }

		public IReadOnlyList<IndexedRequirement> Ingredients { get; }

		public RecipeDefTraits Traits { get; }
	}

	public sealed class IndexedThing
	{
		internal IndexedThing(
			DefIdentity identity,
			IReadOnlyList<DefIdentity> researchPrerequisites,
			IReadOnlyList<IndexedRequirement> constructionCosts,
			DefIdentity? harvestedProduct,
			IReadOnlyList<IndexedRequirement> fuelRequirements,
			ThingDefTraits traits,
			DefIdentity? corpseDefinition)
		{
			Identity = identity;
			ResearchPrerequisites = researchPrerequisites;
			ConstructionCosts = constructionCosts;
			HarvestedProduct = harvestedProduct;
			FuelRequirements = fuelRequirements;
			Traits = traits;
			CorpseDefinition = corpseDefinition;
		}

		public DefIdentity Identity { get; }

		public IReadOnlyList<DefIdentity> ResearchPrerequisites { get; }

		public IReadOnlyList<IndexedRequirement> ConstructionCosts { get; }

		public DefIdentity? HarvestedProduct { get; }

		public IReadOnlyList<IndexedRequirement> FuelRequirements { get; }

		public ThingDefTraits Traits { get; }

		public DefIdentity? CorpseDefinition { get; }
	}

	public sealed class IndexedTerrain
	{
		internal IndexedTerrain(
			DefIdentity identity,
			IReadOnlyList<DefIdentity> researchPrerequisites,
			IReadOnlyList<IndexedRequirement> constructionCosts,
			TerrainDefTraits traits)
		{
			Identity = identity;
			ResearchPrerequisites = researchPrerequisites;
			ConstructionCosts = constructionCosts;
			Traits = traits;
		}

		public DefIdentity Identity { get; }

		public IReadOnlyList<DefIdentity> ResearchPrerequisites { get; }

		public IReadOnlyList<IndexedRequirement> ConstructionCosts { get; }

		public TerrainDefTraits Traits { get; }
	}

	public sealed class IndexedSpecialOpportunity
	{
		internal IndexedSpecialOpportunity(
			SpecialOpportunitySnapshot source,
			DefIdentity project,
			IReadOnlyList<SpecialSubjectSnapshot> subjects)
		{
			Identity = source.Identity;
			Project = project;
			OpportunityType = source.OpportunityType;
			Subjects = subjects;
			RelationOverride = source.RelationOverride;
			ForDirect = source.ForDirect;
			ForAncestor = source.ForAncestor;
			ForDescendant = source.ForDescendant;
			AlternateMode = source.AlternateMode;
			Importance = source.Importance;
			Rare = source.Rare;
			Freebie = source.Freebie;
		}

		public DefIdentity Identity { get; }

		public DefIdentity Project { get; }

		public DefIdentity? OpportunityType { get; }

		public IReadOnlyList<SpecialSubjectSnapshot> Subjects { get; }

		public ResearchRelation? RelationOverride { get; }

		public bool ForDirect { get; }

		public bool ForAncestor { get; }

		public bool ForDescendant { get; }

		public AlternateSubjectMode AlternateMode { get; }

		public float Importance { get; }

		public bool Rare { get; }

		public bool Freebie { get; }
	}

	public sealed class AlternateGroup
	{
		internal AlternateGroup(
			string key,
			AlternateSubjectMode mode,
			IReadOnlyList<DefIdentity> members,
			IReadOnlyList<DefIdentity> sourceDefs,
			bool hasExplicitSource,
			bool hasInferredSource)
		{
			Key = key;
			Mode = mode;
			Members = members;
			SourceDefs = sourceDefs;
			HasExplicitSource = hasExplicitSource;
			HasInferredSource = hasInferredSource;
		}

		public string Key { get; }

		public AlternateSubjectMode Mode { get; }

		public IReadOnlyList<DefIdentity> Members { get; }

		public IReadOnlyList<DefIdentity> SourceDefs { get; }

		public bool HasExplicitSource { get; }

		public bool HasInferredSource { get; }
	}

	public sealed class PrerequisiteCycle
	{
		internal PrerequisiteCycle(IReadOnlyList<DefIdentity> projects)
		{
			Projects = projects;
		}

		public IReadOnlyList<DefIdentity> Projects { get; }
	}

	public enum DefIndexDiagnosticKind
	{
		NullDefinition,
		NullReference,
		MissingProject,
		DuplicateDefinition,
		InvalidRequirement
	}

	public sealed class DefIndexDiagnostic
	{
		internal DefIndexDiagnostic(DefIndexDiagnosticKind kind, string detail, DefIdentity? sourceDef = null)
		{
			Kind = kind;
			Detail = detail;
			SourceDef = sourceDef;
		}

		public DefIndexDiagnosticKind Kind { get; }

		public string Detail { get; }

		public DefIdentity? SourceDef { get; }
	}
}
