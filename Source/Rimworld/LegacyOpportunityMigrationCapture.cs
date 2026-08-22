#nullable enable

using System;
using System.Globalization;
using System.Linq;
using System.Xml;
using PeteTimesSix.ResearchReinvented.Defs;
using PeteTimesSix.ResearchReinvented.Domain;
using PeteTimesSix.ResearchReinvented.Domain.State;
using PeteTimesSix.ResearchReinvented.Opportunities;
using RimWorld;
using Verse;

namespace PeteTimesSix.ResearchReinvented.Rimworld
{
	internal sealed class LegacyOpportunityMigrationCapture
	{
		private LegacyOpportunityMigrationCapture(
			string? project,
			string? type,
			ResearchRelation relation,
			RequirementKind requirementKind,
			string? subjectType,
			string? subject,
			float currentProgress,
			float maximumProgress,
			string? sourceId)
		{
			Project = project; Type = type; Relation = relation; RequirementKind = requirementKind;
			SubjectType = subjectType; Subject = subject; CurrentProgress = currentProgress;
			MaximumProgress = maximumProgress; SourceId = sourceId;
		}

		internal string? Project { get; }
		internal string? Type { get; }
		internal ResearchRelation Relation { get; }
		internal RequirementKind RequirementKind { get; }
		internal string? SubjectType { get; }
		internal string? Subject { get; }
		internal float CurrentProgress { get; }
		internal float MaximumProgress { get; }
		internal string? SourceId { get; }

		internal static LegacyOpportunityMigrationCapture? CaptureCurrentNode()
		{
			var node = Scribe.loader?.curXmlParent;
			if (node == null) return null;
			var relation = Enum.TryParse(node["relation"]?.InnerText, out ResearchRelation parsedRelation) ? parsedRelation : ResearchRelation.Direct;
			var requirement = node["requirement"];
			var requirementClass = requirement?.Attributes?["Class"]?.Value?.Split('.').LastOrDefault() ?? string.Empty;
			var (kind, subjectType, subject) = Requirement(requirementClass, requirement);
			return new LegacyOpportunityMigrationCapture(
				node["project"]?.InnerText,
				node["def"]?.InnerText,
				relation,
				kind,
				subjectType,
				subject,
				Float(node["currentProgress"]?.InnerText),
				Float(node["maximumProgress"]?.InnerText),
				node["loadID"]?.InnerText);
		}

		internal SavedOpportunityState ToSavedState(ResearchOpportunity legacy)
		{
			OpportunitySpec? adapted = null;
			try { if (legacy.IsValid()) adapted = LegacyOpportunityAdapter.ToSpec(legacy); }
			catch { }
			var project = adapted?.Project ?? Identity(nameof(ResearchProjectDef), Project);
			var type = adapted?.Type ?? Identity(nameof(ResearchOpportunityTypeDef), Type);
			var subject = adapted?.Requirement.CanonicalSubject ?? Identity(SubjectType, Subject);
			var kind = adapted?.Requirement.Kind ?? RequirementKind;
			// Phase 5 models social availability as static semantic families.
			// Legacy saves contain transient faction load references; collapsing
			// them here keeps generation static and makes reconciliation stable.
			if (kind == RequirementKind.Faction && type?.DefName == "Brainstorming")
				subject = DefIdentity.Synthetic("player-faction");
			else if (kind == RequirementKind.Faction && type?.DefName == "GainFactionKnowledge")
				subject = DefIdentity.Synthetic("non-player-faction");
			var category = legacy.def?.GetCategory(legacy.relation) is ResearchOpportunityCategoryDef categoryDef
				? new DefIdentity(nameof(ResearchOpportunityCategoryDef), categoryDef.defName)
				: null;
			var stateKind = legacy.def == null ? InferStateKind(Type) : StateKind(legacy.def);
			return new SavedOpportunityState(
				adapted?.Key.Value,
				project,
				type,
				adapted?.Relation ?? Relation,
				kind,
				subject,
				category,
				Finite(CurrentProgress),
				Finite(MaximumProgress),
				stateKind,
				adapted?.Requirement.AlternateSubjects,
				SourceId);
		}

		private static (RequirementKind Kind, string? SubjectType, string? Subject) Requirement(string className, XmlNode? node)
		{
			switch (className)
			{
				case "ROComp_RequiresThing": return (RequirementKind.Thing, nameof(ThingDef), node?["targetDef"]?.InnerText);
				case "ROComp_RequiresTerrain": return (RequirementKind.Terrain, nameof(TerrainDef), node?["terrainDef"]?.InnerText ?? node?["targetDef"]?.InnerText);
				case "ROComp_RequiresRecipe": return (RequirementKind.Recipe, nameof(RecipeDef), node?["targetDef"]?.InnerText);
				case "ROComp_RequiresFaction": return (RequirementKind.Faction, "Semantic", "legacy-faction-" + (node?["faction"]?.InnerText ?? "missing"));
				case "ROComp_RequiresFactionlessPawn": return (RequirementKind.FactionlessPawn, "Semantic", "factionless-pawn");
				case "ROComp_RequiresMatchingBook": return (RequirementKind.Schematic, nameof(ResearchProjectDef), node?["projectDef"]?.InnerText);
				default: return (RequirementKind.None, "Semantic", "none");
			}
		}

		private static PreservedOpportunityStateKind StateKind(ResearchOpportunityTypeDef type)
		{
			if (type.handledBy.HasFlag(HandlingMode.Special_Prototype)) return PreservedOpportunityStateKind.Prototype;
			var special = HandlingMode.Special_OnIngest | HandlingMode.Special_OnIngest_Observable | HandlingMode.Special_Medicine | HandlingMode.Special_Tooling | HandlingMode.Special_Books;
			return (type.handledBy & special) != 0 ? PreservedOpportunityStateKind.Special : PreservedOpportunityStateKind.Ordinary;
		}

		private static PreservedOpportunityStateKind InferStateKind(string? type) =>
			type?.StartsWith("Prototype", StringComparison.Ordinal) == true ? PreservedOpportunityStateKind.Prototype : PreservedOpportunityStateKind.Ordinary;
		private static DefIdentity? Identity(string? type, string? name) => string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(name) ? null : new DefIdentity(type!, name!);
		private static float Float(string? value) => float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0f;
		private static float Finite(float value) => float.IsNaN(value) || float.IsInfinity(value) || value < 0f ? 0f : value;
	}
}
