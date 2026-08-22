#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace PeteTimesSix.ResearchReinvented.Domain
{
	public enum RequirementKind
	{
		None,
		Thing,
		Terrain,
		Recipe,
		Faction,
		FactionlessPawn,
		Schematic
	}

	public enum AlternateSubjectMode
	{
		None,
		Equivalent,
		Similar
	}

	/// <summary>
	/// Immutable semantic requirement data. Runtime lookup and matching remain in
	/// the legacy opportunity comps until later rewrite phases.
	/// </summary>
	public sealed class RequirementSpec : IEquatable<RequirementSpec>
	{
		private static readonly DefIdentity NothingIdentity = DefIdentity.Synthetic("none");
		private static readonly DefIdentity FactionlessPawnIdentity = DefIdentity.Synthetic("factionless-pawn");

		private readonly ReadOnlyCollection<DefIdentity> alternateSubjects;

		private RequirementSpec(
			RequirementKind kind,
			DefIdentity canonicalSubject,
			AlternateSubjectMode alternateMode,
			IEnumerable<DefIdentity>? alternateSubjects)
		{
			Kind = kind;
			CanonicalSubject = canonicalSubject ?? throw new ArgumentNullException(nameof(canonicalSubject));
			AlternateMode = alternateMode;

			var normalizedAlternates = (alternateSubjects ?? Array.Empty<DefIdentity>())
				.Select(subject => subject ?? throw new ArgumentException("Alternate subjects cannot contain null.", nameof(alternateSubjects)))
				.Where(subject => subject != canonicalSubject)
				.Distinct()
				.OrderBy(subject => subject)
				.ToArray();

			if (!SupportsAlternates(kind) && (alternateMode != AlternateSubjectMode.None || normalizedAlternates.Length != 0))
				throw new ArgumentException($"Requirement kind {kind} cannot have alternate subjects.", nameof(alternateSubjects));

			this.alternateSubjects = Array.AsReadOnly(normalizedAlternates);
		}

		public RequirementKind Kind { get; }

		public DefIdentity CanonicalSubject { get; }

		public AlternateSubjectMode AlternateMode { get; }

		public IReadOnlyList<DefIdentity> AlternateSubjects => alternateSubjects;

		public static RequirementSpec Nothing()
		{
			return new RequirementSpec(RequirementKind.None, NothingIdentity, AlternateSubjectMode.None, null);
		}

		public static RequirementSpec FactionlessPawn()
		{
			return new RequirementSpec(RequirementKind.FactionlessPawn, FactionlessPawnIdentity, AlternateSubjectMode.None, null);
		}

		public static RequirementSpec ForThing(
			DefIdentity subject,
			AlternateSubjectMode alternateMode = AlternateSubjectMode.None,
			IEnumerable<DefIdentity>? alternateSubjects = null)
		{
			return new RequirementSpec(RequirementKind.Thing, subject, alternateMode, alternateSubjects);
		}

		public static RequirementSpec ForTerrain(
			DefIdentity subject,
			AlternateSubjectMode alternateMode = AlternateSubjectMode.None,
			IEnumerable<DefIdentity>? alternateSubjects = null)
		{
			return new RequirementSpec(RequirementKind.Terrain, subject, alternateMode, alternateSubjects);
		}

		public static RequirementSpec ForRecipe(
			DefIdentity subject,
			AlternateSubjectMode alternateMode = AlternateSubjectMode.None,
			IEnumerable<DefIdentity>? alternateSubjects = null)
		{
			return new RequirementSpec(RequirementKind.Recipe, subject, alternateMode, alternateSubjects);
		}

		public static RequirementSpec ForFaction(DefIdentity subject)
		{
			return new RequirementSpec(RequirementKind.Faction, subject, AlternateSubjectMode.None, null);
		}

		public static RequirementSpec ForSchematic(DefIdentity project)
		{
			return new RequirementSpec(RequirementKind.Schematic, project, AlternateSubjectMode.None, null);
		}

		public bool Equals(RequirementSpec? other)
		{
			return other != null
				&& Kind == other.Kind
				&& CanonicalSubject == other.CanonicalSubject
				&& AlternateMode == other.AlternateMode
				&& alternateSubjects.SequenceEqual(other.alternateSubjects);
		}

		public override bool Equals(object? obj)
		{
			return Equals(obj as RequirementSpec);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				var hashCode = (int)Kind;
				hashCode = (hashCode * 397) ^ CanonicalSubject.GetHashCode();
				hashCode = (hashCode * 397) ^ (int)AlternateMode;
				foreach (var alternate in alternateSubjects)
					hashCode = (hashCode * 397) ^ alternate.GetHashCode();
				return hashCode;
			}
		}

		private static bool SupportsAlternates(RequirementKind kind)
		{
			return kind == RequirementKind.Thing
				|| kind == RequirementKind.Terrain
				|| kind == RequirementKind.Recipe;
		}
	}
}
