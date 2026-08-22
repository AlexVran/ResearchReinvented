using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PeteTimesSix.ResearchReinvented.Tests;

internal static class Phase0Fixture
{
	internal static JsonObject Load(string relativePath)
	{
		var path = Path.Combine(AppContext.BaseDirectory, "fixtures", relativePath.Replace('/', Path.DirectorySeparatorChar));
		var root = JsonNode.Parse(File.ReadAllText(path)) as JsonObject;
		return root ?? throw new InvalidDataException($"Fixture root is not an object: {path}");
	}

	internal static JsonObject RequiredObject(this JsonObject value, string property)
	{
		return value[property] as JsonObject
			?? throw new InvalidDataException($"Missing object property '{property}'.");
	}

	internal static JsonArray RequiredArray(this JsonObject value, string property)
	{
		return value[property] as JsonArray
			?? throw new InvalidDataException($"Missing array property '{property}'.");
	}

	internal static string RequiredString(this JsonObject value, string property)
	{
		return value[property]?.GetValue<string>()
			?? throw new InvalidDataException($"Missing string property '{property}'.");
	}

	internal static int RequiredInt(this JsonObject value, string property)
	{
		return value[property]?.GetValue<int>()
			?? throw new InvalidDataException($"Missing integer property '{property}'.");
	}

	internal static float RequiredFloat(this JsonObject value, string property)
	{
		return value[property]?.GetValue<float>()
			?? throw new InvalidDataException($"Missing numeric property '{property}'.");
	}

	internal static bool RequiredBool(this JsonObject value, string property)
	{
		return value[property]?.GetValue<bool>()
			?? throw new InvalidDataException($"Missing Boolean property '{property}'.");
	}

	internal static bool? OptionalBool(this JsonObject value, string property)
	{
		return value[property]?.GetValue<bool>();
	}

	internal static IEnumerable<JsonObject> Objects(this JsonArray values)
	{
		foreach (var value in values)
		{
			yield return value as JsonObject
				?? throw new InvalidDataException("Expected an object in a fixture array.");
		}
	}

	internal static string CanonicalDigest(JsonNode node)
	{
		var bytes = Encoding.UTF8.GetBytes(CanonicalJson(node));
		return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
	}

	internal static string CanonicalJson(JsonNode? node)
	{
		if (node == null)
			return "null";

		using var document = JsonDocument.Parse(node.ToJsonString());
		using var stream = new MemoryStream();
		using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
		{
			WriteCanonical(writer, document.RootElement);
		}
		return Encoding.UTF8.GetString(stream.ToArray());
	}

	private static void WriteCanonical(Utf8JsonWriter writer, JsonElement value)
	{
		switch (value.ValueKind)
		{
			case JsonValueKind.Object:
				writer.WriteStartObject();
				foreach (var property in value.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal))
				{
					writer.WritePropertyName(property.Name);
					WriteCanonical(writer, property.Value);
				}
				writer.WriteEndObject();
				break;
			case JsonValueKind.Array:
				writer.WriteStartArray();
				foreach (var item in value.EnumerateArray())
					WriteCanonical(writer, item);
				writer.WriteEndArray();
				break;
			case JsonValueKind.String:
				writer.WriteStringValue(value.GetString());
				break;
			case JsonValueKind.Number:
				writer.WriteRawValue(value.GetRawText(), skipInputValidation: true);
				break;
			case JsonValueKind.True:
				writer.WriteBooleanValue(true);
				break;
			case JsonValueKind.False:
				writer.WriteBooleanValue(false);
				break;
			case JsonValueKind.Null:
				writer.WriteNullValue();
				break;
			default:
				throw new InvalidDataException($"Unsupported JSON value kind: {value.ValueKind}");
		}
	}
}

internal sealed record LegacySaveStateSnapshot(
	string ResearchManagerProject,
	string OpportunityManagerProject,
	int ChangeTicker,
	string[] GeneratedProjects,
	LegacyCategoryStoreSnapshot[] CategoryStores,
	LegacyOpportunityStateSnapshot[] Opportunities,
	string[] PrototypeReferenceIds)
{
	internal static LegacySaveStateSnapshot From(JsonObject fixture)
	{
		var state = fixture.RequiredObject("state");
		return new LegacySaveStateSnapshot(
			state.RequiredString("researchManagerProject"),
			state.RequiredString("opportunityManagerProject"),
			state.RequiredInt("changeTicker"),
			state.RequiredArray("generatedProjects").Select(value => value!.GetValue<string>()).ToArray(),
			state.RequiredArray("categoryStores").Objects().Select(LegacyCategoryStoreSnapshot.From).ToArray(),
			state.RequiredArray("opportunities").Objects().Select(LegacyOpportunityStateSnapshot.From).ToArray(),
			state.RequiredArray("prototypeReferenceIds").Select(value => value?.ToJsonString() ?? "null").ToArray());
	}
}

internal sealed record LegacyCategoryStoreSnapshot(string Project, string Category, float ResearchPoints)
{
	internal static LegacyCategoryStoreSnapshot From(JsonObject value)
	{
		return new LegacyCategoryStoreSnapshot(
			value.RequiredString("project"),
			value.RequiredString("category"),
			value.RequiredFloat("researchPoints"));
	}
}

internal sealed record LegacyOpportunityStateSnapshot(
	string Project,
	string Type,
	string Relation,
	string Category,
	float MaximumProgress,
	float CurrentProgress,
	float Importance,
	int LegacyLoadId,
	bool? ForcedRarePersisted,
	bool? ForcedFreebiePersisted,
	string RequirementKind,
	string RequirementFacts)
{
	internal static LegacyOpportunityStateSnapshot From(JsonObject value)
	{
		var requirement = value.RequiredObject("requirement");
		return new LegacyOpportunityStateSnapshot(
			value.RequiredString("project"),
			value.RequiredString("type"),
			value.RequiredString("relation"),
			value.RequiredString("category"),
			value.RequiredFloat("maximumProgress"),
			value.RequiredFloat("currentProgress"),
			value.RequiredFloat("importance"),
			value.RequiredInt("legacyLoadId"),
			value.OptionalBool("forcedRarePersisted"),
			value.OptionalBool("forcedFreebiePersisted"),
			requirement.RequiredString("kind"),
			Phase0Fixture.CanonicalJson(requirement["facts"]));
	}
}
