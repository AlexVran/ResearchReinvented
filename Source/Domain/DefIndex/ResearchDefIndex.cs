#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using PeteTimesSix.ResearchReinvented.Opportunities;

namespace PeteTimesSix.ResearchReinvented.Domain.DefIndex
{
	public sealed class ResearchDefIndex
	{
		private static readonly IReadOnlyList<SubjectEvidence> NoEvidence = Array.Empty<SubjectEvidence>();
		private static readonly IReadOnlyList<DefIdentity> NoDefinitions = Array.Empty<DefIdentity>();
		private static readonly IReadOnlyList<IndexedSpecialOpportunity> NoSpecials = Array.Empty<IndexedSpecialOpportunity>();
		private readonly IReadOnlyDictionary<DefIdentity, IndexedProject> projectsByIdentity;
		private readonly IReadOnlyDictionary<DefIdentity, IndexedRecipe> recipesByIdentity;
		private readonly IReadOnlyDictionary<DefIdentity, IndexedThing> thingsByIdentity;
		private readonly IReadOnlyDictionary<DefIdentity, IndexedTerrain> terrainsByIdentity;
		private readonly IReadOnlyDictionary<DefIdentity, IReadOnlyList<SubjectEvidence>> evidenceByProject;
		private readonly IReadOnlyDictionary<DefIdentity, IReadOnlyList<DefIdentity>> ancestorsByProject;
		private readonly IReadOnlyDictionary<DefIdentity, IReadOnlyList<DefIdentity>> descendantsByProject;
		private readonly IReadOnlyDictionary<DefIdentity, IReadOnlyList<IndexedSpecialOpportunity>> specialsByProject;
		private readonly IReadOnlyDictionary<string, AlternateGroup> alternateGroupsBySubjectAndMode;
		private readonly IReadOnlyDictionary<DefIdentity, int> thingOrdinals;

		internal ResearchDefIndex(
			IReadOnlyList<IndexedProject> projects,
			IReadOnlyList<IndexedRecipe> recipes,
			IReadOnlyList<IndexedThing> things,
			IReadOnlyList<IndexedTerrain> terrains,
			IReadOnlyList<IndexedSpecialOpportunity> specialOpportunities,
			IReadOnlyList<OpportunityOverrideSnapshot> opportunityOverrides,
			IReadOnlyList<AlternateGroup> alternateGroups,
			IReadOnlyList<PrerequisiteCycle> prerequisiteCycles,
			IReadOnlyList<DefIndexDiagnostic> diagnostics,
			IReadOnlyDictionary<DefIdentity, IReadOnlyList<SubjectEvidence>> evidenceByProject,
			IReadOnlyDictionary<DefIdentity, IReadOnlyList<DefIdentity>> ancestorsByProject,
			IReadOnlyDictionary<DefIdentity, IReadOnlyList<DefIdentity>> descendantsByProject)
		{
			Projects = projects;
			Recipes = recipes;
			Things = things;
			Terrains = terrains;
			SpecialOpportunities = specialOpportunities;
			OpportunityOverrides = opportunityOverrides;
			AlternateGroups = alternateGroups;
			PrerequisiteCycles = prerequisiteCycles;
			Diagnostics = diagnostics;
			projectsByIdentity = ReadOnlyDictionary(projects.ToDictionary(project => project.Identity));
			recipesByIdentity = ReadOnlyDictionary(recipes.ToDictionary(recipe => recipe.Identity));
			thingsByIdentity = ReadOnlyDictionary(things.ToDictionary(thing => thing.Identity));
			terrainsByIdentity = ReadOnlyDictionary(terrains.ToDictionary(terrain => terrain.Identity));
			this.evidenceByProject = evidenceByProject;
			this.ancestorsByProject = ancestorsByProject;
			this.descendantsByProject = descendantsByProject;
			specialsByProject = ReadOnlyDictionary(
				specialOpportunities
					.GroupBy(special => special.Project)
					.ToDictionary(
						group => group.Key,
						group => AsReadOnly(group.OrderBy(special => special.Identity).ToArray())));
			alternateGroupsBySubjectAndMode = ReadOnlyDictionary(
				alternateGroups
					.SelectMany(group => group.Members.Select(member => new { Group = group, Member = member }))
					.ToDictionary(item => AlternateLookupKey(item.Member, item.Group.Mode), item => item.Group, StringComparer.Ordinal));
			thingOrdinals = ReadOnlyDictionary(
				things.Select((thing, ordinal) => new { thing.Identity, Ordinal = ordinal })
					.ToDictionary(item => item.Identity, item => item.Ordinal));
		}

		public IReadOnlyList<IndexedProject> Projects { get; }

		public IReadOnlyList<IndexedRecipe> Recipes { get; }

		public IReadOnlyList<IndexedThing> Things { get; }

		public IReadOnlyList<IndexedTerrain> Terrains { get; }

		public IReadOnlyList<IndexedSpecialOpportunity> SpecialOpportunities { get; }

		public IReadOnlyList<OpportunityOverrideSnapshot> OpportunityOverrides { get; }

		public IReadOnlyList<AlternateGroup> AlternateGroups { get; }

		public IReadOnlyList<PrerequisiteCycle> PrerequisiteCycles { get; }

		public IReadOnlyList<DefIndexDiagnostic> Diagnostics { get; }

		public static ResearchDefIndex Build(IResearchDefSnapshotSource source)
		{
			return new Builder(source ?? throw new ArgumentNullException(nameof(source))).Build();
		}

		public bool TryGetProject(DefIdentity identity, out IndexedProject? project)
		{
			return projectsByIdentity.TryGetValue(identity, out project);
		}

		public bool TryGetRecipe(DefIdentity identity, out IndexedRecipe? recipe)
		{
			return recipesByIdentity.TryGetValue(identity, out recipe);
		}

		public bool TryGetThing(DefIdentity identity, out IndexedThing? thing)
		{
			return thingsByIdentity.TryGetValue(identity, out thing);
		}

		public bool TryGetTerrain(DefIdentity identity, out IndexedTerrain? terrain)
		{
			return terrainsByIdentity.TryGetValue(identity, out terrain);
		}

		public IReadOnlyList<SubjectEvidence> EvidenceFor(DefIdentity project)
		{
			return evidenceByProject.TryGetValue(project, out var evidence) ? evidence : NoEvidence;
		}

		public IReadOnlyList<DefIdentity> AncestorsFor(DefIdentity project)
		{
			return ancestorsByProject.TryGetValue(project, out var ancestors) ? ancestors : NoDefinitions;
		}

		public IReadOnlyList<DefIdentity> DescendantsFor(DefIdentity project)
		{
			return descendantsByProject.TryGetValue(project, out var descendants) ? descendants : NoDefinitions;
		}

		public IReadOnlyList<IndexedSpecialOpportunity> SpecialOpportunitiesFor(DefIdentity project)
		{
			return specialsByProject.TryGetValue(project, out var specials) ? specials : NoSpecials;
		}

		public AlternateGroup? AlternateGroupFor(DefIdentity subject, AlternateSubjectMode mode)
		{
			alternateGroupsBySubjectAndMode.TryGetValue(AlternateLookupKey(subject, mode), out var group);
			return group;
		}

		public bool FilterAllows(CompactDefFilter filter, DefIdentity thing)
		{
			if (filter == null)
				throw new ArgumentNullException(nameof(filter));

			return thingOrdinals.TryGetValue(thing, out var ordinal) && filter.AllowsOrdinal(ordinal);
		}

		public IEnumerable<DefIdentity> DefinitionsAllowedBy(CompactDefFilter filter)
		{
			if (filter == null)
				throw new ArgumentNullException(nameof(filter));

			for (var ordinal = 0; ordinal < Things.Count; ordinal++)
			{
				if (filter.AllowsOrdinal(ordinal))
					yield return Things[ordinal].Identity;
			}
		}

		public string CanonicalSnapshot()
		{
			var lines = new List<string>();
			foreach (var project in Projects)
			{
				lines.Add($"project|{project.Identity}");
				lines.AddRange(project.Prerequisites.Select(edge =>
					$"prerequisite|{edge.Project}|{edge.Kind}|{edge.Prerequisite}"));
				lines.AddRange(project.Unlocks.Select(unlock => $"unlock|{project.Identity}|{unlock}"));
				lines.AddRange(project.RequiredAnalyzed.Select(required => $"required-analysis|{project.Identity}|{required}"));
				if (project.Techprint != null)
					lines.Add($"techprint|{project.Identity}|{project.Techprint}");
			}

			foreach (var recipe in Recipes)
			{
				lines.Add($"recipe|{recipe.Identity}|{recipe.Traits}");
				lines.AddRange(recipe.ResearchPrerequisites.Select(project => $"recipe-project|{recipe.Identity}|{project}"));
				lines.AddRange(recipe.Products.Select(product => $"product|{recipe.Identity}|{product.Definition}|{Float(product.Count)}"));
				lines.AddRange(recipe.Users.Select(user => $"recipe-user|{recipe.Identity}|{user}"));
				lines.AddRange(recipe.Ingredients.Select(requirement => RequirementLine("ingredient", recipe.Identity, requirement)));
			}

			foreach (var thing in Things)
			{
				lines.Add($"thing|{thing.Identity}|{thing.Traits}|{thing.CorpseDefinition}");
				lines.AddRange(thing.ConstructionCosts.Select(requirement => RequirementLine("thing-cost", thing.Identity, requirement)));
				lines.AddRange(thing.FuelRequirements.Select(requirement => RequirementLine("fuel", thing.Identity, requirement)));
				if (thing.HarvestedProduct != null)
					lines.Add($"plant|{thing.Identity}|{thing.HarvestedProduct}");
			}

			foreach (var terrain in Terrains)
			{
				lines.Add($"terrain|{terrain.Identity}|{terrain.Traits}");
				lines.AddRange(terrain.ConstructionCosts.Select(requirement => RequirementLine("terrain-cost", terrain.Identity, requirement)));
			}

			foreach (var special in SpecialOpportunities)
			{
				var subjects = string.Join(",", special.Subjects.Select(subject => $"{subject.Kind}:{subject.Identity}"));
				lines.Add($"special|{special.Identity}|{special.Project}|{special.OpportunityType}|{special.RelationOverride}|{special.ForDirect}|{special.ForAncestor}|{special.ForDescendant}|{special.AlternateMode}|{Float(special.Importance)}|{special.Rare}|{special.Freebie}|{subjects}");
			}

			lines.AddRange(OpportunityOverrides.Select(item =>
				$"override|{item.SourceDef}|{item.Action}|{item.Project}|{item.OpportunityType}|{item.Subject}|{item.Relation}|{item.ReplacementOpportunityType}|{item.ReplacementSubject}|{Float(item.ImportanceMultiplier)}"));

			lines.AddRange(AlternateGroups.Select(group =>
				$"alternate|{group.Key}|{group.Mode}|{group.HasExplicitSource}|{group.HasInferredSource}|{string.Join(",", group.Members)}|{string.Join(",", group.SourceDefs)}"));
			lines.AddRange(PrerequisiteCycles.Select(cycle => $"cycle|{string.Join(",", cycle.Projects)}"));
			foreach (var project in Projects)
			{
				lines.AddRange(EvidenceFor(project.Identity).Select(evidence =>
					$"evidence|{evidence.Project}|{evidence.Relation}|{evidence.Role}|{evidence.Subject}|{evidence.Source}|{evidence.SourceDef}|{Float(evidence.Confidence)}"));
			}
			lines.AddRange(Diagnostics.Select(diagnostic => $"diagnostic|{diagnostic.Kind}|{diagnostic.SourceDef}|{diagnostic.Detail}"));
			return string.Join("\n", lines);
		}

		private static string RequirementLine(string prefix, DefIdentity owner, IndexedRequirement requirement)
		{
			return requirement.FixedDefinition != null
				? $"{prefix}|{owner}|fixed:{requirement.FixedDefinition}|{Float(requirement.Count)}"
				: $"{prefix}|{owner}|filter:{requirement.Filter!.Key}:{requirement.Filter.AllowedDefinitionCount}:{requirement.Filter.EncodedWords.Count}|{Float(requirement.Count)}";
		}

		private static string Float(float value)
		{
			return value.ToString("R", CultureInfo.InvariantCulture);
		}

		private static string AlternateLookupKey(DefIdentity subject, AlternateSubjectMode mode)
		{
			return $"{mode}|{subject.CanonicalValue}";
		}

		private static IReadOnlyDictionary<TKey, TValue> ReadOnlyDictionary<TKey, TValue>(Dictionary<TKey, TValue> source)
			where TKey : notnull
		{
			return new ReadOnlyDictionary<TKey, TValue>(source);
		}

		private static IReadOnlyList<T> AsReadOnly<T>(T[] values)
		{
			return new ReadOnlyCollection<T>(values);
		}

		private sealed class Builder
		{
			private readonly IResearchDefSnapshotSource source;
			private readonly Dictionary<string, DefIndexDiagnostic> diagnostics = new Dictionary<string, DefIndexDiagnostic>(StringComparer.Ordinal);
			private Dictionary<DefIdentity, int> thingOrdinals = new Dictionary<DefIdentity, int>();

			internal Builder(IResearchDefSnapshotSource source)
			{
				this.source = source;
			}

			internal ResearchDefIndex Build()
			{
				var projectSnapshots = SnapshotDefinitions(source.Projects, "project", snapshot => snapshot.Identity, ProjectDescriptor);
				var recipeSnapshots = SnapshotDefinitions(source.Recipes, "recipe", snapshot => snapshot.Identity, RecipeDescriptor);
				var thingSnapshots = SnapshotDefinitions(source.Things, "thing", snapshot => snapshot.Identity, ThingDescriptor);
				var terrainSnapshots = SnapshotDefinitions(source.Terrains, "terrain", snapshot => snapshot.Identity, TerrainDescriptor);
				var specialSnapshots = SnapshotDefinitions(source.SpecialOpportunities, "special opportunity", snapshot => snapshot.Identity, SpecialDescriptor);
				var alternateLinks = SnapshotLinks(source.AlternateLinks);
				var overrides = SnapshotOverrides(source.OpportunityOverrides);

				thingOrdinals = thingSnapshots.Select((thing, ordinal) => new { thing.Identity, Ordinal = ordinal })
					.ToDictionary(item => item.Identity, item => item.Ordinal);

				var recipes = recipeSnapshots.Select(CreateRecipe).ToArray();
				var things = thingSnapshots.Select(CreateThing).ToArray();
				var terrains = terrainSnapshots.Select(CreateTerrain).ToArray();
				var projectIdentities = new HashSet<DefIdentity>(projectSnapshots.Select(project => project.Identity));
				var projectEdges = CreatePrerequisiteEdges(projectSnapshots, projectIdentities);
				var projects = projectSnapshots.Select(project => CreateProject(project, projectEdges)).ToArray();
				var specials = CreateSpecials(specialSnapshots, projectIdentities);
				var alternateGroups = CreateAlternateGroups(alternateLinks);
				var adjacency = CreateAdjacency(projects);
				var reverseAdjacency = ReverseAdjacency(projects, adjacency);
				var ancestors = CreateReachability(projects, adjacency);
				var descendants = CreateReachability(projects, reverseAdjacency);
				var cycles = FindCycles(projects, adjacency, reverseAdjacency);
				var evidence = CreateEvidence(projects, recipes, things, terrains, specials, ancestors, descendants);

				return new ResearchDefIndex(
					AsReadOnly(projects),
					AsReadOnly(recipes),
					AsReadOnly(things),
					AsReadOnly(terrains),
					AsReadOnly(specials),
					AsReadOnly(overrides),
					AsReadOnly(alternateGroups),
					AsReadOnly(cycles),
					AsReadOnly(diagnostics.Values.OrderBy(DiagnosticKey, StringComparer.Ordinal).ToArray()),
					evidence,
					ancestors,
					descendants);
			}

			private T[] SnapshotDefinitions<T>(
				IEnumerable<T?> definitions,
				string name,
				Func<T, DefIdentity> identity,
				Func<T, string> descriptor) where T : class
			{
				var materialized = (definitions ?? Enumerable.Empty<T?>()).ToArray();
				if (materialized.Any(definition => definition == null))
					AddDiagnostic(DefIndexDiagnosticKind.NullDefinition, $"Loaded {name} snapshot was null.");

				var result = new List<T>();
				foreach (var group in materialized.Where(definition => definition != null).Cast<T>().GroupBy(identity).OrderBy(group => group.Key))
				{
					var ordered = group.OrderBy(descriptor, StringComparer.Ordinal).ToArray();
					if (ordered.Length > 1)
						AddDiagnostic(DefIndexDiagnosticKind.DuplicateDefinition, $"Duplicate {name} snapshot {group.Key} was canonicalized.", group.Key);
					result.Add(ordered[0]);
				}

				return result.ToArray();
			}

			private AlternateLinkSnapshot[] SnapshotLinks(IEnumerable<AlternateLinkSnapshot?> links)
			{
				var materialized = (links ?? Enumerable.Empty<AlternateLinkSnapshot?>()).ToArray();
				if (materialized.Any(link => link == null))
					AddDiagnostic(DefIndexDiagnosticKind.NullDefinition, "Loaded alternate link snapshot was null.");
				return materialized
					.Where(link => link != null)
					.Cast<AlternateLinkSnapshot>()
					.OrderBy(AlternateDescriptor, StringComparer.Ordinal)
					.ToArray();
			}

			private OpportunityOverrideSnapshot[] SnapshotOverrides(IEnumerable<OpportunityOverrideSnapshot?> values)
			{
				var materialized = (values ?? Enumerable.Empty<OpportunityOverrideSnapshot?>()).ToArray();
				if (materialized.Any(value => value == null))
					AddDiagnostic(DefIndexDiagnosticKind.NullDefinition, "Loaded opportunity override snapshot was null.");
				var valid = new List<OpportunityOverrideSnapshot>();
				foreach (var value in materialized.Where(value => value != null).Cast<OpportunityOverrideSnapshot>())
				{
					if (float.IsNaN(value.ImportanceMultiplier) || float.IsInfinity(value.ImportanceMultiplier) || value.ImportanceMultiplier < 0f)
					{
						AddDiagnostic(DefIndexDiagnosticKind.InvalidRequirement, $"Opportunity override on {value.SourceDef} has an invalid importance multiplier.", value.SourceDef);
						continue;
					}
					valid.Add(value);
				}
				return valid.OrderBy(OverrideDescriptor, StringComparer.Ordinal).ToArray();
			}

			private IndexedProject CreateProject(ProjectDefSnapshot snapshot, IReadOnlyList<PrerequisiteEdge> allEdges)
			{
				return new IndexedProject(
					snapshot.Identity,
					AsReadOnly(allEdges.Where(edge => edge.Project == snapshot.Identity).ToArray()),
					ValidIdentities(snapshot.Unlocks, snapshot.Identity, "unlock"),
					ValidIdentities(snapshot.RequiredAnalyzed, snapshot.Identity, "required analysis"),
					snapshot.Techprint);
			}

			private PrerequisiteEdge[] CreatePrerequisiteEdges(
				IReadOnlyList<ProjectDefSnapshot> projects,
				ISet<DefIdentity> projectIdentities)
			{
				var edges = new Dictionary<string, PrerequisiteEdge>(StringComparer.Ordinal);
				foreach (var project in projects)
				{
					AddPrerequisites(project.Identity, project.Prerequisites, PrerequisiteKind.Direct, projectIdentities, edges);
					AddPrerequisites(project.Identity, project.HiddenPrerequisites, PrerequisiteKind.Hidden, projectIdentities, edges);
				}

				return edges.Values.OrderBy(PrerequisiteKey, StringComparer.Ordinal).ToArray();
			}

			private void AddPrerequisites(
				DefIdentity project,
				IEnumerable<DefIdentity?> prerequisites,
				PrerequisiteKind kind,
				ISet<DefIdentity> projectIdentities,
				IDictionary<string, PrerequisiteEdge> edges)
			{
				foreach (var prerequisite in prerequisites)
				{
					if (prerequisite == null)
					{
						AddDiagnostic(DefIndexDiagnosticKind.NullReference, $"{project} has a null {kind} prerequisite.", project);
						continue;
					}

					var edge = new PrerequisiteEdge(project, prerequisite, kind);
					edges[PrerequisiteKey(edge)] = edge;
					if (!projectIdentities.Contains(prerequisite))
						AddDiagnostic(DefIndexDiagnosticKind.MissingProject, $"{project} references missing prerequisite {prerequisite}.", project);
				}
			}

			private IndexedRecipe CreateRecipe(RecipeDefSnapshot snapshot)
			{
				var products = snapshot.Products
					.Where(product => ProductIsValid(product, snapshot.Identity))
					.Cast<DefCountSnapshot>()
					.Select(product => new IndexedDefCount(product.Definition!, product.Count))
					.OrderBy(product => $"{product.Definition}|{Float(product.Count)}", StringComparer.Ordinal)
					.ToArray();
				var ingredients = ValidRequirements(snapshot.Ingredients, snapshot.Identity, "ingredient");
				return new IndexedRecipe(
					snapshot.Identity,
					ValidIdentities(snapshot.ResearchPrerequisites, snapshot.Identity, "recipe prerequisite"),
					AsReadOnly(products),
					ValidIdentities(snapshot.Users, snapshot.Identity, "recipe user"),
					ingredients,
					snapshot.Traits);
			}

			private IndexedThing CreateThing(ThingDefSnapshot snapshot)
			{
				return new IndexedThing(
					snapshot.Identity,
					ValidIdentities(snapshot.ResearchPrerequisites, snapshot.Identity, "thing prerequisite"),
					ValidRequirements(snapshot.ConstructionCosts, snapshot.Identity, "construction cost"),
					snapshot.HarvestedProduct,
					ValidRequirements(snapshot.FuelRequirements, snapshot.Identity, "fuel filter"),
					snapshot.Traits,
					snapshot.CorpseDefinition);
			}

			private IndexedTerrain CreateTerrain(TerrainDefSnapshot snapshot)
			{
				return new IndexedTerrain(
					snapshot.Identity,
					ValidIdentities(snapshot.ResearchPrerequisites, snapshot.Identity, "terrain prerequisite"),
					ValidRequirements(snapshot.ConstructionCosts, snapshot.Identity, "construction cost"),
					snapshot.Traits);
			}

			private IndexedSpecialOpportunity[] CreateSpecials(
				IReadOnlyList<SpecialOpportunitySnapshot> snapshots,
				ISet<DefIdentity> projectIdentities)
			{
				var result = new List<IndexedSpecialOpportunity>();
				foreach (var snapshot in snapshots)
				{
					if (snapshot.Project == null)
					{
						AddDiagnostic(DefIndexDiagnosticKind.NullReference, $"Special opportunity {snapshot.Identity} has no project.", snapshot.Identity);
						continue;
					}
					if (!projectIdentities.Contains(snapshot.Project))
					{
						AddDiagnostic(DefIndexDiagnosticKind.MissingProject, $"Special opportunity {snapshot.Identity} references missing project {snapshot.Project}.", snapshot.Identity);
						continue;
					}

					if (snapshot.Subjects.Any(subject => subject == null || subject.Identity == null))
						AddDiagnostic(DefIndexDiagnosticKind.NullReference, $"Special opportunity {snapshot.Identity} contains a null subject.", snapshot.Identity);
					var subjects = snapshot.Subjects
						.Where(subject => subject?.Identity != null)
						.Cast<SpecialSubjectSnapshot>()
						.OrderBy(subject => $"{subject.Kind}|{subject.Identity}", StringComparer.Ordinal)
						.ToArray();
					result.Add(new IndexedSpecialOpportunity(snapshot, snapshot.Project, AsReadOnly(subjects)));
				}

				return result.OrderBy(special => special.Identity).ToArray();
			}

			private IReadOnlyList<DefIdentity> ValidIdentities(
				IEnumerable<DefIdentity?> values,
				DefIdentity sourceDef,
				string role)
			{
				var materialized = values.ToArray();
				if (materialized.Any(value => value == null))
					AddDiagnostic(DefIndexDiagnosticKind.NullReference, $"{sourceDef} contains a null {role} reference.", sourceDef);
				return AsReadOnly(materialized.Where(value => value != null).Cast<DefIdentity>().Distinct().OrderBy(value => value).ToArray());
			}

			private IReadOnlyList<IndexedRequirement> ValidRequirements(
				IEnumerable<DefRequirementSnapshot?> values,
				DefIdentity sourceDef,
				string role)
			{
				var materialized = values.ToArray();
				if (materialized.Any(value => value == null))
					AddDiagnostic(DefIndexDiagnosticKind.NullReference, $"{sourceDef} contains a null {role} entry.", sourceDef);

				var result = new List<IndexedRequirement>();
				foreach (var value in materialized.Where(value => value != null).Cast<DefRequirementSnapshot>())
				{
					if (!value.IsFilter && value.FixedDefinition == null)
					{
						AddDiagnostic(DefIndexDiagnosticKind.InvalidRequirement, $"{sourceDef} contains a fixed {role} with no definition.", sourceDef);
						continue;
					}

					result.Add(value.IsFilter
						? new IndexedRequirement(null, CreateFilter(value.AllowedDefinitions, sourceDef, role), value.Count)
						: new IndexedRequirement(value.FixedDefinition, null, value.Count));
				}

				return AsReadOnly(result.OrderBy(RequirementKey, StringComparer.Ordinal).ToArray());
			}

			private CompactDefFilter CreateFilter(
				IEnumerable<DefIdentity?> allowedDefinitions,
				DefIdentity sourceDef,
				string role)
			{
				var allowed = allowedDefinitions.ToArray();
				if (allowed.Any(definition => definition == null))
					AddDiagnostic(DefIndexDiagnosticKind.NullReference, $"{sourceDef} contains a null definition in its {role} filter.", sourceDef);
				var valid = allowed.Where(definition => definition != null).Cast<DefIdentity>().Distinct().OrderBy(definition => definition).ToArray();
				var words = new ulong[(thingOrdinals.Count + 63) / 64];
				var encoded = new List<string>();
				foreach (var definition in valid)
				{
					encoded.Add(definition.CanonicalValue);
					if (thingOrdinals.TryGetValue(definition, out var ordinal))
						words[ordinal / 64] |= 1UL << (ordinal % 64);
				}

				return new CompactDefFilter($"df1-{Hash(string.Join("\n", encoded))}", valid.Length, words);
			}

			private IReadOnlyDictionary<DefIdentity, IReadOnlyList<DefIdentity>> CreateReachability(
				IReadOnlyList<IndexedProject> projects,
				IReadOnlyDictionary<DefIdentity, IReadOnlyList<DefIdentity>> adjacency)
			{
				var result = new Dictionary<DefIdentity, IReadOnlyList<DefIdentity>>();
				foreach (var project in projects)
				{
					var reached = new HashSet<DefIdentity>();
					var pending = new Stack<DefIdentity>(adjacency[project.Identity].Reverse());
					while (pending.Count > 0)
					{
						var next = pending.Pop();
						if (!reached.Add(next))
							continue;
						foreach (var adjacent in adjacency[next].Reverse())
							pending.Push(adjacent);
					}
					reached.Remove(project.Identity);
					result[project.Identity] = AsReadOnly(reached.OrderBy(identity => identity).ToArray());
				}

				return ReadOnlyDictionary(result);
			}

			private IReadOnlyDictionary<DefIdentity, IReadOnlyList<DefIdentity>> CreateAdjacency(IReadOnlyList<IndexedProject> projects)
			{
				var known = new HashSet<DefIdentity>(projects.Select(project => project.Identity));
				return ReadOnlyDictionary(projects.ToDictionary(
					project => project.Identity,
					project => AsReadOnly(project.Prerequisites.Select(edge => edge.Prerequisite).Where(known.Contains).Distinct().OrderBy(identity => identity).ToArray())));
			}

			private IReadOnlyDictionary<DefIdentity, IReadOnlyList<DefIdentity>> ReverseAdjacency(
				IReadOnlyList<IndexedProject> projects,
				IReadOnlyDictionary<DefIdentity, IReadOnlyList<DefIdentity>> adjacency)
			{
				var reverse = projects.ToDictionary(project => project.Identity, _ => new HashSet<DefIdentity>());
				foreach (var pair in adjacency)
				{
					foreach (var prerequisite in pair.Value)
						reverse[prerequisite].Add(pair.Key);
				}
				return ReadOnlyDictionary(reverse.ToDictionary(
					pair => pair.Key,
					pair => AsReadOnly(pair.Value.OrderBy(identity => identity).ToArray())));
			}

			private PrerequisiteCycle[] FindCycles(
				IReadOnlyList<IndexedProject> projects,
				IReadOnlyDictionary<DefIdentity, IReadOnlyList<DefIdentity>> adjacency,
				IReadOnlyDictionary<DefIdentity, IReadOnlyList<DefIdentity>> reverseAdjacency)
			{
				var visited = new HashSet<DefIdentity>();
				var finishOrder = new List<DefIdentity>();
				foreach (var project in projects.Select(project => project.Identity))
				{
					if (visited.Contains(project))
						continue;
					var pending = new Stack<Tuple<DefIdentity, bool>>();
					pending.Push(Tuple.Create(project, false));
					while (pending.Count > 0)
					{
						var current = pending.Pop();
						if (current.Item2)
						{
							finishOrder.Add(current.Item1);
							continue;
						}
						if (!visited.Add(current.Item1))
							continue;
						pending.Push(Tuple.Create(current.Item1, true));
						foreach (var next in adjacency[current.Item1].Reverse())
						{
							if (!visited.Contains(next))
								pending.Push(Tuple.Create(next, false));
						}
					}
				}

				visited.Clear();
				var cycles = new List<PrerequisiteCycle>();
				foreach (var project in finishOrder.AsEnumerable().Reverse())
				{
					if (!visited.Add(project))
						continue;
					var component = new List<DefIdentity>();
					var pending = new Stack<DefIdentity>();
					pending.Push(project);
					while (pending.Count > 0)
					{
						var current = pending.Pop();
						component.Add(current);
						foreach (var next in reverseAdjacency[current].Reverse())
						{
							if (visited.Add(next))
								pending.Push(next);
						}
					}

					component.Sort();
					if (component.Count > 1 || adjacency[component[0]].Contains(component[0]))
						cycles.Add(new PrerequisiteCycle(AsReadOnly(component.ToArray())));
				}

				return cycles.OrderBy(cycle => cycle.Projects[0]).ToArray();
			}

			private AlternateGroup[] CreateAlternateGroups(IReadOnlyList<AlternateLinkSnapshot> links)
			{
				var groups = new List<AlternateGroup>();
				foreach (var mode in new[] { AlternateSubjectMode.Equivalent, AlternateSubjectMode.Similar })
				{
					var valid = links.Where(link => link.Mode == mode).ToArray();
					var union = new UnionFind();
					foreach (var link in valid)
					{
						if (link.Original == null || link.Alternate == null)
						{
							AddDiagnostic(DefIndexDiagnosticKind.NullReference, $"Alternate source {link.SourceDef} contains a null {mode} endpoint.", link.SourceDef);
							continue;
						}
						if (!string.Equals(link.Original.DefType, link.Alternate.DefType, StringComparison.Ordinal))
						{
							AddDiagnostic(DefIndexDiagnosticKind.InvalidRequirement, $"Alternate source {link.SourceDef} joins different Def types.", link.SourceDef);
							continue;
						}
						union.Union(link.Original, link.Alternate);
					}

					foreach (var members in union.Groups().Where(group => group.Count > 1))
					{
						var memberSet = new HashSet<DefIdentity>(members);
						var sources = valid.Where(link => link.Original != null && link.Alternate != null && memberSet.Contains(link.Original) && memberSet.Contains(link.Alternate)).ToArray();
						var sourceDefs = AsReadOnly(sources.Select(link => link.SourceDef).Distinct().OrderBy(identity => identity).ToArray());
						var canonicalMembers = AsReadOnly(members.OrderBy(identity => identity).ToArray());
						groups.Add(new AlternateGroup(
							$"ag1-{mode.ToString().ToLowerInvariant()}-{Hash(string.Join("\n", canonicalMembers.Select(member => member.CanonicalValue)))}",
							mode,
							canonicalMembers,
							sourceDefs,
							sources.Any(source => !source.Inferred),
							sources.Any(source => source.Inferred)));
					}
				}

				return groups.OrderBy(group => group.Key, StringComparer.Ordinal).ToArray();
			}

			private IReadOnlyDictionary<DefIdentity, IReadOnlyList<SubjectEvidence>> CreateEvidence(
				IReadOnlyList<IndexedProject> projects,
				IReadOnlyList<IndexedRecipe> recipes,
				IReadOnlyList<IndexedThing> things,
				IReadOnlyList<IndexedTerrain> terrains,
				IReadOnlyList<IndexedSpecialOpportunity> specials,
				IReadOnlyDictionary<DefIdentity, IReadOnlyList<DefIdentity>> ancestors,
				IReadOnlyDictionary<DefIdentity, IReadOnlyList<DefIdentity>> descendants)
			{
				var baseEvidence = projects.ToDictionary(project => project.Identity, _ => new List<SubjectEvidence>());
				var thingsByIdentity = things.ToDictionary(thing => thing.Identity);
				var terrainsByIdentity = terrains.ToDictionary(terrain => terrain.Identity);
				foreach (var project in projects)
				{
					AddEvidence(baseEvidence, project.Identity, project.Identity, ResearchRelation.Direct, SubjectRole.Project, EvidenceSource.ProjectDefinition, project.Identity, 1f);
					foreach (var edge in project.Prerequisites)
					{
						var source = edge.Kind == PrerequisiteKind.Direct
							? EvidenceSource.Prerequisite
							: EvidenceSource.HiddenPrerequisite;
						AddEvidence(baseEvidence, project.Identity, edge.Prerequisite, ResearchRelation.Ancestor, SubjectRole.Project, source, project.Identity, 1f);
					}
					foreach (var unlock in project.Unlocks)
					{
						AddEvidence(baseEvidence, project.Identity, unlock, ResearchRelation.Direct, SubjectRole.Unlock, EvidenceSource.ProjectDefinition, project.Identity, 1f);
						AddBuildableEvidence(baseEvidence, project.Identity, unlock, thingsByIdentity, terrainsByIdentity, EvidenceSource.ProjectDefinition);
					}
					foreach (var required in project.RequiredAnalyzed)
					{
						AddEvidence(baseEvidence, project.Identity, required, ResearchRelation.Direct, SubjectRole.AnalysisRequirement, EvidenceSource.RequiredAnalysis, project.Identity, 1f);
						AddBuildableEvidence(baseEvidence, project.Identity, required, thingsByIdentity, terrainsByIdentity, EvidenceSource.RequiredAnalysis);
					}
					if (project.Techprint != null)
						AddEvidence(baseEvidence, project.Identity, project.Techprint, ResearchRelation.Direct, SubjectRole.Techprint, EvidenceSource.ProjectDefinition, project.Identity, 1f);
				}

				var projectByIdentity = projects.ToDictionary(project => project.Identity);
				foreach (var recipe in recipes)
				{
					var owningProjects = new HashSet<DefIdentity>(recipe.ResearchPrerequisites.Where(projectByIdentity.ContainsKey));
					foreach (var project in projects.Where(project => project.Unlocks.Contains(recipe.Identity)))
						owningProjects.Add(project.Identity);
					foreach (var project in owningProjects)
						AddRecipeEvidence(baseEvidence, project, recipe);
				}

				foreach (var project in projects)
				{
					var unlockedThings = new HashSet<DefIdentity>(project.Unlocks.Where(unlock => string.Equals(unlock.DefType, "ThingDef", StringComparison.Ordinal)));
					foreach (var recipe in recipes.Where(recipe => recipe.Products.Any(product => unlockedThings.Contains(product.Definition))))
					{
						foreach (var user in recipe.Users)
							AddEvidence(baseEvidence, project.Identity, user, ResearchRelation.Direct, SubjectRole.ProductionFacility, EvidenceSource.RecipeDefinition, recipe.Identity, 0.8f);
					}
				}

				foreach (var special in specials)
				{
					var relations = SpecialRelations(special).Distinct().OrderBy(relation => relation).ToArray();
					var subjects = special.Subjects.Count > 0
						? special.Subjects.Select(subject => subject.Identity!).ToArray()
						: new[] { special.Identity };
					foreach (var relation in relations)
					{
						foreach (var subject in subjects)
							AddEvidence(baseEvidence, special.Project, subject, relation, SubjectRole.Special, EvidenceSource.SpecialOpportunity, special.Identity, 1f);
					}
				}

				var result = new Dictionary<DefIdentity, IReadOnlyList<SubjectEvidence>>();
				foreach (var project in projects)
				{
					var evidence = new List<SubjectEvidence>(baseEvidence[project.Identity]);
					foreach (var ancestor in ancestors[project.Identity])
						evidence.AddRange(baseEvidence[ancestor].Where(item => item.Relation == ResearchRelation.Direct).Select(item => RelateEvidence(project.Identity, item, ResearchRelation.Ancestor)));
					foreach (var descendant in descendants[project.Identity])
						evidence.AddRange(baseEvidence[descendant].Where(item => item.Relation == ResearchRelation.Direct).Select(item => RelateEvidence(project.Identity, item, ResearchRelation.Descendant)));
					result[project.Identity] = AsReadOnly(evidence
						.GroupBy(EvidenceKey, StringComparer.Ordinal)
						.Select(group => group.First())
						.OrderBy(EvidenceKey, StringComparer.Ordinal)
						.ToArray());
				}

				return ReadOnlyDictionary(result);
			}

			private void AddBuildableEvidence(
				IDictionary<DefIdentity, List<SubjectEvidence>> evidence,
				DefIdentity project,
				DefIdentity buildable,
				IReadOnlyDictionary<DefIdentity, IndexedThing> things,
				IReadOnlyDictionary<DefIdentity, IndexedTerrain> terrains,
				EvidenceSource source)
			{
				if (things.TryGetValue(buildable, out var thing))
				{
					foreach (var cost in thing.ConstructionCosts)
						AddEvidence(evidence, project, cost.EvidenceSubject, ResearchRelation.Direct, SubjectRole.CostMaterial, source, thing.Identity, 0.8f);
					if (thing.HarvestedProduct != null)
						AddEvidence(evidence, project, thing.HarvestedProduct, ResearchRelation.Direct, SubjectRole.Plant, EvidenceSource.ThingDefinition, thing.Identity, 0.9f);
					foreach (var fuel in thing.FuelRequirements)
						AddEvidence(evidence, project, fuel.EvidenceSubject, ResearchRelation.Direct, SubjectRole.Fuel, EvidenceSource.CompProperties, thing.Identity, 0.8f);
				}
				else if (terrains.TryGetValue(buildable, out var terrain))
				{
					foreach (var cost in terrain.ConstructionCosts)
						AddEvidence(evidence, project, cost.EvidenceSubject, ResearchRelation.Direct, SubjectRole.CostMaterial, source, terrain.Identity, 0.8f);
				}
			}

			private static void AddRecipeEvidence(
				IDictionary<DefIdentity, List<SubjectEvidence>> evidence,
				DefIdentity project,
				IndexedRecipe recipe)
			{
				AddEvidence(evidence, project, recipe.Identity, ResearchRelation.Direct, SubjectRole.Recipe, EvidenceSource.RecipeDefinition, recipe.Identity, 1f);
				foreach (var product in recipe.Products)
					AddEvidence(evidence, project, product.Definition, ResearchRelation.Direct, SubjectRole.Product, EvidenceSource.RecipeDefinition, recipe.Identity, 1f);
				foreach (var user in recipe.Users)
					AddEvidence(evidence, project, user, ResearchRelation.Direct, SubjectRole.ProductionFacility, EvidenceSource.RecipeDefinition, recipe.Identity, 0.9f);
				foreach (var ingredient in recipe.Ingredients)
					AddEvidence(evidence, project, ingredient.EvidenceSubject, ResearchRelation.Direct, SubjectRole.Ingredient, EvidenceSource.RecipeDefinition, recipe.Identity, 0.8f);
			}

			private static IEnumerable<ResearchRelation> SpecialRelations(IndexedSpecialOpportunity special)
			{
				if (special.RelationOverride.HasValue)
				{
					if (special.ForDirect || special.ForAncestor || special.ForDescendant)
						yield return special.RelationOverride.Value;
					yield break;
				}
				if (special.ForDirect)
					yield return ResearchRelation.Direct;
				if (special.ForAncestor)
					yield return ResearchRelation.Ancestor;
				if (special.ForDescendant)
					yield return ResearchRelation.Descendant;
			}

			private static SubjectEvidence RelateEvidence(DefIdentity project, SubjectEvidence evidence, ResearchRelation relation)
			{
				return new SubjectEvidence(project, evidence.Subject, relation, evidence.Role, evidence.Source, evidence.SourceDef, evidence.Confidence);
			}

			private static void AddEvidence(
				IDictionary<DefIdentity, List<SubjectEvidence>> evidence,
				DefIdentity project,
				DefIdentity subject,
				ResearchRelation relation,
				SubjectRole role,
				EvidenceSource source,
				DefIdentity sourceDef,
				float confidence)
			{
				if (evidence.TryGetValue(project, out var target))
					target.Add(new SubjectEvidence(project, subject, relation, role, source, sourceDef, confidence));
			}

			private bool ProductIsValid(DefCountSnapshot? product, DefIdentity recipe)
			{
				if (product?.Definition != null)
					return true;
				AddDiagnostic(DefIndexDiagnosticKind.NullReference, $"{recipe} contains a null recipe product.", recipe);
				return false;
			}

			private void AddDiagnostic(DefIndexDiagnosticKind kind, string detail, DefIdentity? sourceDef = null)
			{
				var diagnostic = new DefIndexDiagnostic(kind, detail, sourceDef);
				diagnostics[DiagnosticKey(diagnostic)] = diagnostic;
			}

			private static string ProjectDescriptor(ProjectDefSnapshot project)
			{
				return $"{project.Identity}|{Identities(project.Prerequisites)}|{Identities(project.HiddenPrerequisites)}|{Identities(project.Unlocks)}|{Identities(project.RequiredAnalyzed)}|{project.Techprint}";
			}

			private static string RecipeDescriptor(RecipeDefSnapshot recipe)
			{
				return $"{recipe.Identity}|{Identities(recipe.ResearchPrerequisites)}|{string.Join(",", recipe.Products.Select(product => product?.Definition?.CanonicalValue))}|{Identities(recipe.Users)}|{recipe.Ingredients.Count}|{recipe.Traits}";
			}

			private static string ThingDescriptor(ThingDefSnapshot thing)
			{
				return $"{thing.Identity}|{Identities(thing.ResearchPrerequisites)}|{thing.ConstructionCosts.Count}|{thing.HarvestedProduct}|{thing.FuelRequirements.Count}|{thing.Traits}|{thing.CorpseDefinition}";
			}

			private static string TerrainDescriptor(TerrainDefSnapshot terrain)
			{
				return $"{terrain.Identity}|{Identities(terrain.ResearchPrerequisites)}|{terrain.ConstructionCosts.Count}|{terrain.Traits}";
			}

			private static string SpecialDescriptor(SpecialOpportunitySnapshot special)
			{
				return $"{special.Identity}|{special.Project}|{special.OpportunityType}|{special.RelationOverride}|{special.ForDirect}|{special.ForAncestor}|{special.ForDescendant}|{special.AlternateMode}|{Float(special.Importance)}|{special.Rare}|{special.Freebie}|{string.Join(",", special.Subjects.Select(subject => subject?.Identity?.CanonicalValue))}";
			}

			private static string AlternateDescriptor(AlternateLinkSnapshot link)
			{
				return $"{link.Mode}|{link.Original}|{link.Alternate}|{link.SourceDef}|{link.Inferred}";
			}

			private static string OverrideDescriptor(OpportunityOverrideSnapshot item)
			{
				return $"{item.SourceDef}|{item.Action}|{item.Project}|{item.OpportunityType}|{item.Subject}|{item.Relation}|{item.ReplacementOpportunityType}|{item.ReplacementSubject}|{Float(item.ImportanceMultiplier)}";
			}

			private static string Identities(IEnumerable<DefIdentity?> identities)
			{
				return string.Join(",", identities.Select(identity => identity?.CanonicalValue ?? "<null>").OrderBy(value => value, StringComparer.Ordinal));
			}

			private static string RequirementKey(IndexedRequirement requirement)
			{
				return $"{requirement.FixedDefinition}|{requirement.Filter?.Key}|{Float(requirement.Count)}";
			}

			private static string PrerequisiteKey(PrerequisiteEdge edge)
			{
				return $"{edge.Project}|{edge.Kind}|{edge.Prerequisite}";
			}

			private static string EvidenceKey(SubjectEvidence evidence)
			{
				return $"{evidence.Project}|{evidence.Relation}|{evidence.Role}|{evidence.Subject}|{evidence.Source}|{evidence.SourceDef}|{Float(evidence.Confidence)}";
			}

			private static string DiagnosticKey(DefIndexDiagnostic diagnostic)
			{
				return $"{diagnostic.Kind}|{diagnostic.SourceDef}|{diagnostic.Detail}";
			}

			private sealed class UnionFind
			{
				private readonly Dictionary<DefIdentity, DefIdentity> parent = new Dictionary<DefIdentity, DefIdentity>();

				internal void Union(DefIdentity left, DefIdentity right)
				{
					var leftRoot = Find(left);
					var rightRoot = Find(right);
					if (leftRoot == rightRoot)
						return;
					if (leftRoot.CompareTo(rightRoot) <= 0)
						parent[rightRoot] = leftRoot;
					else
						parent[leftRoot] = rightRoot;
				}

				internal IEnumerable<IReadOnlyList<DefIdentity>> Groups()
				{
					// Find performs path compression. Snapshot the keys so Mono's Dictionary
					// enumerator is not invalidated when an existing parent value changes.
					return parent.Keys.ToArray().GroupBy(Find).Select(group => AsReadOnly(group.OrderBy(identity => identity).ToArray()));
				}

				private DefIdentity Find(DefIdentity item)
				{
					if (!parent.TryGetValue(item, out var current))
					{
						parent[item] = item;
						return item;
					}
					var path = new List<DefIdentity>();
					while (current != parent[current])
					{
						path.Add(current);
						current = parent[current];
					}
					foreach (var member in path)
						parent[member] = current;
					return current;
				}
			}
		}

		private static string Hash(string value)
		{
			using (var sha256 = SHA256.Create())
			{
				var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(value));
				var result = new StringBuilder(bytes.Length * 2);
				foreach (var valueByte in bytes)
					result.Append(valueByte.ToString("x2", CultureInfo.InvariantCulture));
				return result.ToString();
			}
		}
	}
}
