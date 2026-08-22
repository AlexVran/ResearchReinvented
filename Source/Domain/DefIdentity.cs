#nullable enable

using System;
using System.Text;

namespace PeteTimesSix.ResearchReinvented.Domain
{
	/// <summary>
	/// A RimWorld definition identity represented without retaining a loaded Def.
	/// </summary>
	public sealed class DefIdentity : IEquatable<DefIdentity>, IComparable<DefIdentity>
	{
		public DefIdentity(string defType, string defName)
		{
			DefType = NormalizeRequired(defType, nameof(defType));
			DefName = NormalizeRequired(defName, nameof(defName));
		}

		public string DefType { get; }

		public string DefName { get; }

		public string CanonicalValue => $"{Escape(DefType)}/{Escape(DefName)}";

		public static DefIdentity Synthetic(string name)
		{
			return new DefIdentity("Semantic", name);
		}

		public bool Equals(DefIdentity? other)
		{
			return other != null
				&& string.Equals(DefType, other.DefType, StringComparison.Ordinal)
				&& string.Equals(DefName, other.DefName, StringComparison.Ordinal);
		}

		public override bool Equals(object? obj)
		{
			return Equals(obj as DefIdentity);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				return (StringComparer.Ordinal.GetHashCode(DefType) * 397)
					^ StringComparer.Ordinal.GetHashCode(DefName);
			}
		}

		public int CompareTo(DefIdentity? other)
		{
			if (other == null)
				return 1;

			var typeComparison = string.Compare(DefType, other.DefType, StringComparison.Ordinal);
			return typeComparison != 0
				? typeComparison
				: string.Compare(DefName, other.DefName, StringComparison.Ordinal);
		}

		public override string ToString()
		{
			return CanonicalValue;
		}

		public static bool operator ==(DefIdentity? left, DefIdentity? right)
		{
			return Equals(left, right);
		}

		public static bool operator !=(DefIdentity? left, DefIdentity? right)
		{
			return !Equals(left, right);
		}

		private static string NormalizeRequired(string value, string parameterName)
		{
			if (string.IsNullOrWhiteSpace(value))
				throw new ArgumentException("Definition identity components cannot be empty.", parameterName);

			return value.Normalize(NormalizationForm.FormC);
		}

		private static string Escape(string value)
		{
			var bytes = Encoding.UTF8.GetBytes(value);
			var escaped = new StringBuilder(bytes.Length);
			foreach (var valueByte in bytes)
			{
				if ((valueByte >= (byte)'a' && valueByte <= (byte)'z')
					|| (valueByte >= (byte)'A' && valueByte <= (byte)'Z')
					|| (valueByte >= (byte)'0' && valueByte <= (byte)'9')
					|| valueByte == (byte)'-'
					|| valueByte == (byte)'.'
					|| valueByte == (byte)'_'
					|| valueByte == (byte)'~')
				{
					escaped.Append((char)valueByte);
				}
				else
				{
					escaped.Append('%');
					escaped.Append(valueByte.ToString("X2"));
				}
			}

			return escaped.ToString();
		}
	}
}
