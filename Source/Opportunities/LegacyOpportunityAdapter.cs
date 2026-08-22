#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using PeteTimesSix.ResearchReinvented.Defs;
using PeteTimesSix.ResearchReinvented.Domain;
using PeteTimesSix.ResearchReinvented.OpportunityComps;
using PeteTimesSix.ResearchReinvented.Rimworld;
using RimWorld;
using Verse;

namespace PeteTimesSix.ResearchReinvented.Opportunities
{
	/// <summary>
	/// Temporary bridge between Phase 3 domain values and the legacy runtime
	/// opportunity model. The legacy generator remains authoritative.
	/// </summary>
	internal static class LegacyOpportunityAdapter
	{
		internal static OpportunitySpec ToSpec(ResearchOpportunity legacy)
		{
			if (legacy == null)
				throw new ArgumentNullException(nameof(legacy));

			var project = IdentityFor<ResearchProjectDef>(legacy.project);
			var type = IdentityFor<ResearchOpportunityTypeDef>(legacy.def);
			var requirement = RequirementFromLegacy(legacy.requirement);
			var reason = new GenerationReason(
				GenerationReasonKind.LegacyGenerator,
				EvidenceSource.LegacyAdapter,
				project,
				legacy.debug_source);

			return new OpportunitySpec(
				project,
				type,
				legacy.relation,
				requirement,
				legacy.importance,
				legacy.IsRare,
				legacy.IsFreebie,
				new[] { reason });
		}

		internal static bool TryCreateLegacy(
			OpportunitySpec spec,
			out ResearchOpportunity? opportunity,
			out RejectionReason? rejection)
		{
			if (spec == null)
				throw new ArgumentNullException(nameof(spec));

			opportunity = null;
			rejection = null;

			if (!TryResolveDef(spec.Project, out ResearchProjectDef? project))
			{
				rejection = MissingDefinition(spec.Project);
				return false;
			}

			if (!TryResolveDef(spec.Type, out ResearchOpportunityTypeDef? type))
			{
				rejection = MissingDefinition(spec.Type);
				return false;
			}

			if (!TryCreateLegacyRequirement(spec.Requirement, out var requirement, out rejection))
				return false;
			if ((!spec.Rare && requirement!.IsRare) || (!spec.Freebie && requirement.IsFreebie))
			{
				rejection = new RejectionReason(
					RejectionReasonKind.InvalidRequirement,
					"The loaded requirement forces a rare or freebie flag that the specification does not contain.",
					spec.Requirement.CanonicalSubject);
				return false;
			}

			var primaryReason = spec.Reasons.FirstOrDefault();
			var debugSource = primaryReason?.Detail ?? primaryReason?.Kind.ToString() ?? "domain adapter";
			opportunity = new ResearchOpportunity(
				project,
				type,
				spec.Relation,
				requirement,
				debugSource,
				spec.Importance,
				spec.Rare,
				spec.Freebie);
			return true;
		}

		private static RequirementSpec RequirementFromLegacy(ResearchOpportunityComp requirement)
		{
			if (requirement == null)
				throw new ArgumentNullException(nameof(requirement));

			switch (requirement)
			{
				case ROComp_RequiresNothing:
					return RequirementSpec.Nothing();
				case ROComp_RequiresThing thing:
					return RequirementSpec.ForThing(
						IdentityFor<ThingDef>(thing.PrimaryThingDef),
						ToDomainMode(thing.altsMode),
						IdentitiesFor<ThingDef>(thing.Alternates));
				case ROComp_RequiresTerrain terrain:
					return RequirementSpec.ForTerrain(
						IdentityFor<TerrainDef>(terrain.PrimaryTerrainDef),
						ToDomainMode(terrain.altsMode),
						IdentitiesFor<TerrainDef>(terrain.Alternates));
				case ROComp_RequiresRecipe recipe:
					return RequirementSpec.ForRecipe(
						IdentityFor<RecipeDef>(recipe.PrimaryRecipeDef),
						ToDomainMode(recipe.altsMode),
						IdentitiesFor<RecipeDef>(recipe.Alternates));
				case ROComp_RequiresFaction faction:
					return RequirementSpec.ForFaction(IdentityFor<FactionDef>(faction.faction?.def));
				case ROComp_RequiresFactionlessPawn:
					return RequirementSpec.FactionlessPawn();
				case ROComp_RequiresSchematicWithProject schematic:
					return RequirementSpec.ForSchematic(IdentityFor<ResearchProjectDef>(schematic.projectDef));
				default:
					throw new NotSupportedException($"Unsupported legacy requirement type {requirement.GetType().FullName}.");
			}
		}

		private static bool TryCreateLegacyRequirement(
			RequirementSpec spec,
			out ResearchOpportunityComp? requirement,
			out RejectionReason? rejection)
		{
			requirement = null;
			rejection = null;

			switch (spec.Kind)
			{
				case RequirementKind.None:
					requirement = new ROComp_RequiresNothing();
					return true;
				case RequirementKind.Thing:
					if (!TryResolveDef(spec.CanonicalSubject, out ThingDef? thing))
						break;
					requirement = new ROComp_RequiresThing(thing, ToLegacyMode(spec.AlternateMode));
					return true;
				case RequirementKind.Terrain:
					if (!TryResolveDef(spec.CanonicalSubject, out TerrainDef? terrain))
						break;
					requirement = new ROComp_RequiresTerrain(terrain, ToLegacyMode(spec.AlternateMode));
					return true;
				case RequirementKind.Recipe:
					if (!TryResolveDef(spec.CanonicalSubject, out RecipeDef? recipe))
						break;
					requirement = new ROComp_RequiresRecipe(recipe, ToLegacyMode(spec.AlternateMode));
					return true;
				case RequirementKind.Faction:
					var faction = ResolveFaction(spec.CanonicalSubject);
					if (faction == null)
						break;
					requirement = new ROComp_RequiresFaction(faction);
					return true;
				case RequirementKind.FactionlessPawn:
					requirement = new ROComp_RequiresFactionlessPawn();
					return true;
				case RequirementKind.Schematic:
					if (!TryResolveDef(spec.CanonicalSubject, out ResearchProjectDef? project))
						break;
					requirement = new ROComp_RequiresSchematicWithProject(project);
					return true;
				default:
					rejection = new RejectionReason(
						RejectionReasonKind.UnsupportedLegacyRequirement,
						$"Legacy execution cannot construct requirement kind {spec.Kind}.",
						spec.CanonicalSubject);
					return false;
			}

			rejection = MissingDefinition(spec.CanonicalSubject);
			return false;
		}

		private static bool TryResolveDef<TDef>(DefIdentity identity, out TDef? result)
			where TDef : Def
		{
			if (!string.Equals(identity.DefType, typeof(TDef).Name, StringComparison.Ordinal))
			{
				result = null;
				return false;
			}

			result = ResearchRuntimeServices.Current.AllDefsListForReading<TDef>()
				.FirstOrDefault(def => string.Equals(def.defName, identity.DefName, StringComparison.Ordinal));
			return result != null;
		}

		private static Faction? ResolveFaction(DefIdentity identity)
		{
			if (!string.Equals(identity.DefType, nameof(FactionDef), StringComparison.Ordinal))
				return null;

			var services = ResearchRuntimeServices.Current;
			return Enumerable.Repeat(services.PlayerFaction, 1)
				.Concat(services.ResearchFactions)
				.Where(faction => faction?.def != null)
				.FirstOrDefault(faction => string.Equals(faction.def.defName, identity.DefName, StringComparison.Ordinal));
		}

		private static DefIdentity IdentityFor<TDef>(TDef? def)
			where TDef : Def
		{
			if (def == null || string.IsNullOrWhiteSpace(def.defName))
				throw new ArgumentException($"Cannot create a {typeof(TDef).Name} identity from a missing Def.", nameof(def));

			return new DefIdentity(typeof(TDef).Name, def.defName);
		}

		private static IEnumerable<DefIdentity> IdentitiesFor<TDef>(IEnumerable<TDef>? defs)
			where TDef : Def
		{
			return (defs ?? Enumerable.Empty<TDef>()).Select(IdentityFor<TDef>);
		}

		private static AlternateSubjectMode ToDomainMode(AlternatesMode mode)
		{
			switch (mode)
			{
				case AlternatesMode.NONE:
					return AlternateSubjectMode.None;
				case AlternatesMode.EQUIVALENT:
					return AlternateSubjectMode.Equivalent;
				case AlternatesMode.SIMILAR:
					return AlternateSubjectMode.Similar;
				default:
					throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
			}
		}

		private static AlternatesMode ToLegacyMode(AlternateSubjectMode mode)
		{
			switch (mode)
			{
				case AlternateSubjectMode.None:
					return AlternatesMode.NONE;
				case AlternateSubjectMode.Equivalent:
					return AlternatesMode.EQUIVALENT;
				case AlternateSubjectMode.Similar:
					return AlternatesMode.SIMILAR;
				default:
					throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
			}
		}

		private static RejectionReason MissingDefinition(DefIdentity identity)
		{
			return new RejectionReason(
				RejectionReasonKind.MissingDefinition,
				$"No loaded {identity.DefType} named {identity.DefName} exists for the legacy adapter.",
				identity);
		}
	}
}
