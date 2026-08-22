#nullable enable

using System;
using PeteTimesSix.ResearchReinvented.Opportunities;

namespace PeteTimesSix.ResearchReinvented.Domain
{
	/// <summary>
	/// Versioned, human-readable identity for one semantic opportunity.
	/// </summary>
	public sealed class OpportunityKey : IEquatable<OpportunityKey>, IComparable<OpportunityKey>
	{
		private const string Prefix = "rr1|";

		private OpportunityKey(string value)
		{
			Value = value;
		}

		public string Value { get; }

		public static OpportunityKey Create(
			DefIdentity project,
			DefIdentity type,
			ResearchRelation relation,
			RequirementSpec requirement)
		{
			if (project == null)
				throw new ArgumentNullException(nameof(project));
			if (type == null)
				throw new ArgumentNullException(nameof(type));
			if (requirement == null)
				throw new ArgumentNullException(nameof(requirement));

			return new OpportunityKey(
				$"{Prefix}project={project.CanonicalValue}"
				+ $"|type={type.CanonicalValue}"
				+ $"|relation={relation}"
				+ $"|requirement={requirement.Kind}"
				+ $"|subject={requirement.CanonicalSubject.CanonicalValue}");
		}

		public static OpportunityKey Parse(string value)
		{
			if (string.IsNullOrWhiteSpace(value) || !value.StartsWith(Prefix, StringComparison.Ordinal))
				throw new FormatException("Opportunity keys must use the rr1 semantic-key format.");

			return new OpportunityKey(value);
		}

		public bool Equals(OpportunityKey? other)
		{
			return other != null && string.Equals(Value, other.Value, StringComparison.Ordinal);
		}

		public override bool Equals(object? obj)
		{
			return Equals(obj as OpportunityKey);
		}

		public override int GetHashCode()
		{
			return StringComparer.Ordinal.GetHashCode(Value);
		}

		public int CompareTo(OpportunityKey? other)
		{
			return other == null ? 1 : string.Compare(Value, other.Value, StringComparison.Ordinal);
		}

		public override string ToString()
		{
			return Value;
		}

		public static bool operator ==(OpportunityKey? left, OpportunityKey? right)
		{
			return Equals(left, right);
		}

		public static bool operator !=(OpportunityKey? left, OpportunityKey? right)
		{
			return !Equals(left, right);
		}
	}
}
