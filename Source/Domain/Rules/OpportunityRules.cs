#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using PeteTimesSix.ResearchReinvented.Domain.DefIndex;
using PeteTimesSix.ResearchReinvented.Opportunities;

namespace PeteTimesSix.ResearchReinvented.Domain.Rules
{
	public static class OpportunityTypeIds
	{
		public static DefIdentity Of(string defName) => new DefIdentity("ResearchOpportunityTypeDef", defName);
	}

	public sealed class OpportunityCandidate
	{
		public OpportunityCandidate(string ruleId, OpportunitySpec spec, float evidenceConfidence = 1f, float staticFeasibility = 1f)
		{
			RuleId = string.IsNullOrWhiteSpace(ruleId) ? throw new ArgumentException("A candidate needs a rule id.", nameof(ruleId)) : ruleId;
			Spec = spec ?? throw new ArgumentNullException(nameof(spec));
			if (float.IsNaN(evidenceConfidence) || float.IsInfinity(evidenceConfidence) || evidenceConfidence < 0f || evidenceConfidence > 1f)
				throw new ArgumentOutOfRangeException(nameof(evidenceConfidence), "Evidence confidence must be between zero and one.");
			if (float.IsNaN(staticFeasibility) || float.IsInfinity(staticFeasibility) || staticFeasibility < 0f || staticFeasibility > 1f)
				throw new ArgumentOutOfRangeException(nameof(staticFeasibility), "Static feasibility must be between zero and one.");
			EvidenceConfidence = evidenceConfidence;
			StaticFeasibility = staticFeasibility;
		}

		public string RuleId { get; }
		public OpportunitySpec Spec { get; }
		public float EvidenceConfidence { get; }
		public float StaticFeasibility { get; }
	}

	public sealed class OpportunityRuleResult
	{
		public OpportunityRuleResult(IEnumerable<OpportunityCandidate>? candidates = null, IEnumerable<RejectionReason>? rejections = null)
		{
			Candidates = Array.AsReadOnly((candidates ?? Array.Empty<OpportunityCandidate>()).ToArray());
			Rejections = Array.AsReadOnly((rejections ?? Array.Empty<RejectionReason>()).ToArray());
		}

		public IReadOnlyList<OpportunityCandidate> Candidates { get; }
		public IReadOnlyList<RejectionReason> Rejections { get; }
	}

	public sealed class OpportunityRuleContext
	{
		public OpportunityRuleContext(ResearchDefIndex index, DefIdentity project)
		{
			Index = index ?? throw new ArgumentNullException(nameof(index));
			Project = project ?? throw new ArgumentNullException(nameof(project));
			Evidence = index.EvidenceFor(project);
		}

		public ResearchDefIndex Index { get; }
		public DefIdentity Project { get; }
		public IReadOnlyList<SubjectEvidence> Evidence { get; }
	}

	public interface IOpportunityRule
	{
		string Id { get; }
		int Order { get; }
		OpportunityRuleResult Evaluate(OpportunityRuleContext context);
	}

	public sealed class OpportunityRuleRegistry
	{
		private readonly IReadOnlyList<IOpportunityRule> rules;

		public OpportunityRuleRegistry(IEnumerable<IOpportunityRule> rules)
		{
			this.rules = Array.AsReadOnly((rules ?? throw new ArgumentNullException(nameof(rules)))
				.OrderBy(rule => rule.Order).ThenBy(rule => rule.Id, StringComparer.Ordinal).ToArray());
			if (this.rules.GroupBy(rule => rule.Id, StringComparer.Ordinal).Any(group => group.Count() > 1))
				throw new ArgumentException("Opportunity rule ids must be unique.", nameof(rules));
		}

		public IReadOnlyList<IOpportunityRule> Rules => rules;

		public static OpportunityRuleRegistry CreateDefault() => new OpportunityRuleRegistry(new IOpportunityRule[]
		{
			new TheoryAndBookRule(), new SocialRule(), new DirectAnalysisRule(), new ContextAnalysisRule(),
			new PlantRule(), new TerrainRule(), new PrototypeRule(), new ExplicitSpecialRule()
		});

		public OpportunityRuleResult Generate(ResearchDefIndex index, DefIdentity project)
		{
			if (!index.TryGetProject(project, out _))
				return new OpportunityRuleResult(rejections: new[] { new RejectionReason(RejectionReasonKind.MissingDefinition, $"Unknown research project {project}.", project) });

			var context = new OpportunityRuleContext(index, project);
			var candidates = new List<OpportunityCandidate>();
			var rejections = new List<RejectionReason>();
			foreach (var rule in rules)
			{
				var result = rule.Evaluate(context);
				candidates.AddRange(result.Candidates);
				rejections.AddRange(result.Rejections);
			}

			ApplyOverrides(index, project, candidates, rejections);
			return new OpportunityRuleResult(
				candidates.OrderBy(candidate => RuleOrder(candidate.RuleId)).ThenBy(candidate => candidate.Spec.Key).ThenBy(candidate => candidate.Spec.Importance).ToArray(),
				rejections.OrderBy(reason => reason.Kind).ThenBy(reason => reason.SourceDef).ThenBy(reason => reason.Detail, StringComparer.Ordinal).ToArray());
		}

		private int RuleOrder(string id) => rules.FirstOrDefault(rule => rule.Id == id)?.Order ?? int.MaxValue;

		private static void ApplyOverrides(ResearchDefIndex index, DefIdentity project, List<OpportunityCandidate> candidates, List<RejectionReason> rejections)
		{
			foreach (var item in index.OpportunityOverrides.Where(item => item.Project == null || item.Project == project))
			{
				if (item.Action == OpportunityOverrideAction.Force)
				{
					if (item.OpportunityType == null || item.Project == null)
					{
						rejections.Add(new RejectionReason(RejectionReasonKind.InvalidRequirement, "A force override requires an explicit project and opportunity type.", item.SourceDef));
						continue;
					}
					var requirement = RequirementFor(index, item.Subject);
					if (requirement == null)
					{
						rejections.Add(new RejectionReason(RejectionReasonKind.InvalidRequirement, "A force override has an unsupported subject type.", item.SourceDef));
						continue;
					}
					candidates.Add(new OpportunityCandidate("metadata.force", Spec(project, item.OpportunityType, item.Relation ?? ResearchRelation.Direct, requirement, item.ImportanceMultiplier, false, true,
						new GenerationReason(GenerationReasonKind.ExplicitMetadata, EvidenceSource.ModExtension, item.SourceDef, "forced"))));
					continue;
				}

				for (var position = candidates.Count - 1; position >= 0; position--)
				{
					var candidate = candidates[position];
					if (!Matches(item, candidate.Spec)) continue;
					if (item.Action == OpportunityOverrideAction.Suppress)
					{
						candidates.RemoveAt(position);
						rejections.Add(new RejectionReason(RejectionReasonKind.SuppressedByMetadata, $"{candidate.Spec.Key} was suppressed by explicit metadata.", item.SourceDef));
					}
					else
					{
						var replacementRequirement = item.ReplacementSubject == null ? candidate.Spec.Requirement : RequirementFor(index, item.ReplacementSubject);
						if (replacementRequirement == null)
						{
							rejections.Add(new RejectionReason(RejectionReasonKind.InvalidRequirement, "A replacement override has an unsupported subject type.", item.SourceDef));
							continue;
						}
						var importance = item.Action == OpportunityOverrideAction.Reweight ? candidate.Spec.Importance * item.ImportanceMultiplier : candidate.Spec.Importance;
						candidates[position] = new OpportunityCandidate("metadata." + item.Action.ToString().ToLowerInvariant(), Spec(project,
							item.ReplacementOpportunityType ?? candidate.Spec.Type, candidate.Spec.Relation, replacementRequirement, importance,
							candidate.Spec.Rare, candidate.Spec.Freebie, candidate.Spec.Reasons.Concat(new[] { new GenerationReason(GenerationReasonKind.ExplicitMetadata, EvidenceSource.ModExtension, item.SourceDef, item.Action.ToString()) }).ToArray()),
							candidate.EvidenceConfidence, candidate.StaticFeasibility);
					}
				}
			}
		}

		private static bool Matches(OpportunityOverrideSnapshot item, OpportunitySpec spec) =>
			(item.OpportunityType == null || item.OpportunityType == spec.Type)
			&& (item.Subject == null || item.Subject == spec.Requirement.CanonicalSubject)
			&& (!item.Relation.HasValue || item.Relation.Value == spec.Relation);

		internal static RequirementSpec? RequirementFor(ResearchDefIndex index, DefIdentity? subject, AlternateSubjectMode mode = AlternateSubjectMode.None)
		{
			if (subject == null) return RequirementSpec.Nothing();
			var alternates = index.AlternateGroupFor(subject, mode)?.Members;
			if (subject.DefType == "ThingDef") return RequirementSpec.ForThing(subject, mode, alternates);
			if (subject.DefType == "TerrainDef") return RequirementSpec.ForTerrain(subject, mode, alternates);
			if (subject.DefType == "RecipeDef") return RequirementSpec.ForRecipe(subject, mode, alternates);
			return null;
		}

		internal static OpportunitySpec Spec(DefIdentity project, DefIdentity type, ResearchRelation relation, RequirementSpec requirement,
			float importance, bool rare, bool freebie, params GenerationReason[] reasons) =>
			new OpportunitySpec(project, type, relation, requirement, importance, rare, freebie, reasons);
	}

	internal abstract class OpportunityRuleBase : IOpportunityRule
	{
		public abstract string Id { get; }
		public abstract int Order { get; }
		public abstract OpportunityRuleResult Evaluate(OpportunityRuleContext context);

		protected OpportunityCandidate Candidate(OpportunityRuleContext context, string type, SubjectEvidence evidence, RequirementSpec requirement, GenerationReasonKind reasonKind) =>
			new OpportunityCandidate(Id, OpportunityRuleRegistry.Spec(context.Project, OpportunityTypeIds.Of(type), evidence.Relation, requirement, 1f, false, true,
				new GenerationReason(reasonKind, evidence.Source, evidence.SourceDef, evidence.Role.ToString())), evidence.Confidence);

		protected static bool Strong(SubjectEvidence evidence) => evidence.Role == SubjectRole.AnalysisRequirement || evidence.Role == SubjectRole.Unlock || evidence.Role == SubjectRole.Product;
		protected static bool Has<T>(T value, T flag) where T : struct => (Convert.ToInt32(value) & Convert.ToInt32(flag)) != 0;
		protected static RequirementSpec ThingRequirement(OpportunityRuleContext context, SubjectEvidence evidence) =>
			OpportunityRuleRegistry.RequirementFor(context.Index, evidence.Subject, evidence.Relation == ResearchRelation.Direct ? AlternateSubjectMode.Similar : AlternateSubjectMode.Equivalent)!;
	}

	internal sealed class TheoryAndBookRule : OpportunityRuleBase
	{
		public override string Id => "theory-books";
		public override int Order => 100;
		public override OpportunityRuleResult Evaluate(OpportunityRuleContext context)
		{
			var reason = new GenerationReason(GenerationReasonKind.Project, EvidenceSource.ProjectDefinition, context.Project);
			var candidates = new List<OpportunityCandidate>
			{
				new OpportunityCandidate(Id, OpportunityRuleRegistry.Spec(context.Project, OpportunityTypeIds.Of("BasicResearch"), ResearchRelation.Direct, RequirementSpec.Nothing(), 1f, false, true, reason)),
				new OpportunityCandidate(Id, OpportunityRuleRegistry.Spec(context.Project, OpportunityTypeIds.Of("SchematicStudy"), ResearchRelation.Direct, RequirementSpec.ForSchematic(context.Project), 1f, false, true, reason))
			};
			if (context.Index.TryGetProject(context.Project, out var project) && project?.Techprint != null)
				candidates.Add(new OpportunityCandidate(Id, OpportunityRuleRegistry.Spec(context.Project, OpportunityTypeIds.Of("AnalyseTechprint"), ResearchRelation.Direct, RequirementSpec.ForThing(project.Techprint), 1f, false, true,
					new GenerationReason(GenerationReasonKind.RequiredAnalysis, EvidenceSource.ProjectDefinition, context.Project, "techprint"))));
			return new OpportunityRuleResult(candidates);
		}
	}

	internal sealed class SocialRule : OpportunityRuleBase
	{
		public override string Id => "social";
		public override int Order => 200;
		public override OpportunityRuleResult Evaluate(OpportunityRuleContext context)
		{
			var reason = new GenerationReason(GenerationReasonKind.Faction, EvidenceSource.ProjectDefinition, context.Project, "static opportunity; runtime availability deferred");
			return new OpportunityRuleResult(new[]
			{
				new OpportunityCandidate(Id, OpportunityRuleRegistry.Spec(context.Project, OpportunityTypeIds.Of("Brainstorming"), ResearchRelation.Direct, RequirementSpec.ForFaction(DefIdentity.Synthetic("player-faction")), 1f, false, true, reason)),
				new OpportunityCandidate(Id, OpportunityRuleRegistry.Spec(context.Project, OpportunityTypeIds.Of("GainFactionKnowledge"), ResearchRelation.Direct, RequirementSpec.ForFaction(DefIdentity.Synthetic("non-player-faction")), 1f, false, true, reason)),
				new OpportunityCandidate(Id, OpportunityRuleRegistry.Spec(context.Project, OpportunityTypeIds.Of("GainFactionlessKnowledge"), ResearchRelation.Direct, RequirementSpec.FactionlessPawn(), 1f, false, true, reason))
			});
		}
	}

	internal sealed class DirectAnalysisRule : OpportunityRuleBase
	{
		public override string Id => "direct-analysis";
		public override int Order => 300;
		public override OpportunityRuleResult Evaluate(OpportunityRuleContext context)
		{
			var candidates = new List<OpportunityCandidate>();
			foreach (var evidence in context.Evidence.Where(Strong))
			{
				if (!context.Index.TryGetThing(evidence.Subject, out var thing) || thing == null) continue;
				var traits = thing.Traits;
				string type;
				if (Has(traits, ThingDefTraits.Medicine)) type = "AnalyseMedicine";
				else if (Has(traits, ThingDefTraits.Drug)) type = "AnalyseDrug";
				else if (Has(traits, ThingDefTraits.Pawn)) type = Has(traits, ThingDefTraits.FleshPawn) ? "AnalysePawn" : "AnalysePawnNonFlesh";
				else if (Has(traits, ThingDefTraits.Corpse))
				{
					var flesh = context.Index.Things.Any(candidate => candidate.CorpseDefinition == thing.Identity && Has(candidate.Traits, ThingDefTraits.FleshPawn));
					type = flesh ? "AnalyseDissect" : "AnalyseDissectNonFlesh";
				}
				else if (Has(traits, ThingDefTraits.Plant)) type = "AnalysePlant";
				else if (Has(traits, ThingDefTraits.RawFood)) type = "AnalyseRawFood";
				else if (Has(traits, ThingDefTraits.Ingestible)) type = "AnalyseFood";
				else type = "Analyse";
				candidates.Add(Candidate(context, type, evidence, ThingRequirement(context, evidence), GenerationReasonKind.RequiredAnalysis));
				if (Has(traits, ThingDefTraits.Pawn) && thing.CorpseDefinition != null)
				{
					var corpseRequirement = OpportunityRuleRegistry.RequirementFor(context.Index, thing.CorpseDefinition,
						evidence.Relation == ResearchRelation.Direct ? AlternateSubjectMode.Similar : AlternateSubjectMode.Equivalent)!;
					candidates.Add(Candidate(context, Has(traits, ThingDefTraits.FleshPawn) ? "AnalyseDissect" : "AnalyseDissectNonFlesh",
						evidence, corpseRequirement, GenerationReasonKind.RequiredAnalysis));
				}
				if (Has(traits, ThingDefTraits.Drug) && Has(traits, ThingDefTraits.Ingestible))
					candidates.Add(Candidate(context, "TrialDrug", evidence, ThingRequirement(context, evidence), GenerationReasonKind.RequiredAnalysis));
			}
			return new OpportunityRuleResult(candidates);
		}
	}

	internal sealed class ContextAnalysisRule : OpportunityRuleBase
	{
		public override string Id => "context-analysis";
		public override int Order => 400;
		public override OpportunityRuleResult Evaluate(OpportunityRuleContext context)
		{
			var candidates = new List<OpportunityCandidate>();
			foreach (var evidence in context.Evidence.Where(evidence => evidence.Subject.DefType == "ThingDef"))
			{
				if (!context.Index.TryGetThing(evidence.Subject, out var thing) || thing == null) continue;
				if (evidence.Role == SubjectRole.ProductionFacility && context.Index.TryGetProject(context.Project, out var project) && project!.Unlocks.Contains(thing.Identity)) continue;
				string? type = null;
				switch (evidence.Role)
				{
					case SubjectRole.ProductionFacility: type = Has(thing.Traits, ThingDefTraits.Pawn) ? (Has(thing.Traits, ThingDefTraits.FleshPawn) ? "AnalysePawn" : "AnalysePawnNonFlesh") : "AnalyseProductionFacility"; break;
					case SubjectRole.Ingredient:
						var clinicalUse = context.Index.TryGetRecipe(evidence.SourceDef, out var sourceRecipe)
							&& sourceRecipe != null && Has(sourceRecipe.Traits, RecipeDefTraits.Surgery);
						type = Has(thing.Traits, ThingDefTraits.Medicine) && clinicalUse ? "AnalyseMedicine"
							: Has(thing.Traits, ThingDefTraits.Drug) ? "AnalyseDrug"
							: Has(thing.Traits, ThingDefTraits.Ingestible) ? "AnalyseIngredientsFood" : "AnalyseIngredients";
						break;
					case SubjectRole.CostMaterial: type = "AnalyseIngredients"; break;
					case SubjectRole.Fuel:
						type = Has(thing.Traits, ThingDefTraits.Drug) ? "AnalyseFuelDrug" : Has(thing.Traits, ThingDefTraits.Ingestible) ? "AnalyseFuelFood" : Has(thing.Traits, ThingDefTraits.Flammable) ? "AnalyseFuelFlammable" : "AnalyseFuel";
						break;
				}
				if (type != null) candidates.Add(Candidate(context, type, evidence, ThingRequirement(context, evidence), GenerationReasonKind.Ingredient));
				if (evidence.Role == SubjectRole.ProductionFacility && Has(thing.Traits, ThingDefTraits.Pawn) && thing.CorpseDefinition != null)
					candidates.Add(Candidate(context, Has(thing.Traits, ThingDefTraits.FleshPawn) ? "AnalyseDissect" : "AnalyseDissectNonFlesh", evidence,
						OpportunityRuleRegistry.RequirementFor(context.Index, thing.CorpseDefinition, AlternateSubjectMode.Equivalent)!, GenerationReasonKind.Ingredient));
			}
			return new OpportunityRuleResult(candidates);
		}
	}

	internal sealed class PlantRule : OpportunityRuleBase
	{
		public override string Id => "plants";
		public override int Order => 500;
		public override OpportunityRuleResult Evaluate(OpportunityRuleContext context) => new OpportunityRuleResult(
			context.Evidence.Where(evidence => evidence.Role == SubjectRole.Plant && evidence.Subject.DefType == "ThingDef")
				.Select(evidence => Candidate(context, "AnalyseHarvestProduct", evidence, ThingRequirement(context, evidence), GenerationReasonKind.Plant)).ToArray());
	}

	internal sealed class TerrainRule : OpportunityRuleBase
	{
		public override string Id => "terrain";
		public override int Order => 600;
		public override OpportunityRuleResult Evaluate(OpportunityRuleContext context)
		{
			var candidates = new List<OpportunityCandidate>();
			foreach (var evidence in context.Evidence.Where(evidence => Strong(evidence) && evidence.Subject.DefType == "TerrainDef"))
			{
				if (!context.Index.TryGetTerrain(evidence.Subject, out var terrain) || terrain == null) continue;
				var type = Has(terrain.Traits, TerrainDefTraits.Soil) ? "AnalyseSoil" : Has(terrain.Traits, TerrainDefTraits.PlayerBuildable) ? "AnalyseFloor" : "AnalyseTerrain";
				candidates.Add(Candidate(context, type, evidence, OpportunityRuleRegistry.RequirementFor(context.Index, evidence.Subject, AlternateSubjectMode.Similar)!, GenerationReasonKind.Unlock));
			}
			return new OpportunityRuleResult(candidates);
		}
	}

	internal sealed class PrototypeRule : OpportunityRuleBase
	{
		public override string Id => "prototypes";
		public override int Order => 700;
		public override OpportunityRuleResult Evaluate(OpportunityRuleContext context)
		{
			var candidates = new List<OpportunityCandidate>();
			var ancestors = new HashSet<DefIdentity>(context.Index.AncestorsFor(context.Project)) { context.Project };
			foreach (var evidence in context.Evidence.Where(evidence => evidence.Relation == ResearchRelation.Direct && (evidence.Role == SubjectRole.Recipe || evidence.Role == SubjectRole.Unlock)))
			{
				if (context.Index.TryGetRecipe(evidence.Subject, out var recipe) && recipe != null)
				{
					var gated = recipe.ResearchPrerequisites.Contains(context.Project) && recipe.ResearchPrerequisites.All(ancestors.Contains);
					if (!gated || Has(recipe.Traits, RecipeDefTraits.Blacklisted) || !Has(recipe.Traits, RecipeDefTraits.Meaningful)) continue;
					var type = Has(recipe.Traits, RecipeDefTraits.Surgery) ? "PrototypeSurgery" : recipe.Products.Count == 1 ? "PrototypeProduction" : null;
					if (type != null) candidates.Add(Candidate(context, type, evidence, OpportunityRuleRegistry.RequirementFor(context.Index, recipe.Identity, AlternateSubjectMode.Equivalent)!, GenerationReasonKind.Recipe));
				}
				else if (context.Index.TryGetThing(evidence.Subject, out var thing) && thing != null
					&& Has(thing.Traits, ThingDefTraits.PlayerBuildable) && !Has(thing.Traits, ThingDefTraits.Haulable)
					&& !Has(thing.Traits, ThingDefTraits.InstantBuild) && !Has(thing.Traits, ThingDefTraits.Plant)
					&& thing.ResearchPrerequisites.Contains(context.Project) && thing.ResearchPrerequisites.All(ancestors.Contains))
					candidates.Add(Candidate(context, "PrototypeConstruction", evidence, OpportunityRuleRegistry.RequirementFor(context.Index, thing.Identity, AlternateSubjectMode.Equivalent)!, GenerationReasonKind.Unlock));
				else if (context.Index.TryGetTerrain(evidence.Subject, out var terrain) && terrain != null
					&& Has(terrain.Traits, TerrainDefTraits.PlayerBuildable) && terrain.ResearchPrerequisites.Contains(context.Project) && terrain.ResearchPrerequisites.All(ancestors.Contains))
					candidates.Add(Candidate(context, "PrototypeTerrainConstruction", evidence, OpportunityRuleRegistry.RequirementFor(context.Index, terrain.Identity, AlternateSubjectMode.Equivalent)!, GenerationReasonKind.Unlock));
			}
			return new OpportunityRuleResult(candidates);
		}
	}

	internal sealed class ExplicitSpecialRule : OpportunityRuleBase
	{
		public override string Id => "explicit-specials";
		public override int Order => 800;
		public override OpportunityRuleResult Evaluate(OpportunityRuleContext context)
		{
			var candidates = new List<OpportunityCandidate>();
			var rejections = new List<RejectionReason>();
			foreach (var special in context.Index.SpecialOpportunitiesFor(context.Project))
			{
				var relations = Relations(special).ToArray();
				var subjects = special.Subjects.Count == 0 ? new DefIdentity?[] { null } : special.Subjects.Select(subject => subject.Identity).ToArray();
				foreach (var relation in relations)
				foreach (var subject in subjects)
				{
					var requirement = OpportunityRuleRegistry.RequirementFor(context.Index, subject, special.AlternateMode);
					var type = special.OpportunityType ?? InferType(context.Index, subject);
					if (requirement == null || type == null)
					{
						rejections.Add(new RejectionReason(RejectionReasonKind.InvalidRequirement, $"Special opportunity {special.Identity} cannot infer a supported type and requirement.", special.Identity));
						continue;
					}
					candidates.Add(new OpportunityCandidate(Id, OpportunityRuleRegistry.Spec(context.Project, type, relation, requirement, special.Importance, special.Rare, special.Freebie,
						new GenerationReason(GenerationReasonKind.ExplicitMetadata, EvidenceSource.SpecialOpportunity, special.Identity))));
				}
			}
			return new OpportunityRuleResult(candidates, rejections);
		}

		private static IEnumerable<ResearchRelation> Relations(IndexedSpecialOpportunity special)
		{
			if (special.RelationOverride.HasValue)
			{
				if (special.ForDirect || special.ForAncestor || special.ForDescendant)
					yield return special.RelationOverride.Value;
				yield break;
			}
			if (special.ForDirect) yield return ResearchRelation.Direct;
			if (special.ForAncestor) yield return ResearchRelation.Ancestor;
			if (special.ForDescendant) yield return ResearchRelation.Descendant;
		}

		private static DefIdentity? InferType(ResearchDefIndex index, DefIdentity? subject)
		{
			if (subject == null) return null;
			if (index.TryGetRecipe(subject, out var recipe) && recipe != null)
				return OpportunityTypeIds.Of(Has(recipe.Traits, RecipeDefTraits.Surgery) ? "PrototypeSurgery" : "PrototypeProduction");
			if (index.TryGetTerrain(subject, out _)) return OpportunityTypeIds.Of("AnalyseTerrain");
			if (index.TryGetThing(subject, out var thing) && thing != null)
				return OpportunityTypeIds.Of(Has(thing.Traits, ThingDefTraits.Medicine) ? "AnalyseMedicine" : Has(thing.Traits, ThingDefTraits.Drug) ? "AnalyseDrug" : "Analyse");
			return null;
		}
	}
}
