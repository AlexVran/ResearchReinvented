using Xunit;

namespace PeteTimesSix.ResearchReinvented.Tests;

public sealed class PrototypeRuntimeContractTests
{
	[Fact]
	public void ArgonicSensitiveCompletionUsesInvocationLocalPrefixPostfixState()
	{
		var source = Contract("prototypes", "Frame_CompleteConstruction_Patches.cs");
		Assert.Contains("[HarmonyPrefix]", source, StringComparison.Ordinal);
		Assert.Contains("[HarmonyPostfix]", source, StringComparison.Ordinal);
		Assert.Contains("CompletionState __state", source, StringComparison.Ordinal);
		Assert.Contains("ThingsOfDef(thingDef)", source, StringComparison.Ordinal);
		Assert.Contains("TransitionThing", source, StringComparison.Ordinal);
		Assert.DoesNotContain("HarmonyTranspiler", source, StringComparison.Ordinal);
		Assert.DoesNotContain("GenSpawn.Spawn", source, StringComparison.Ordinal);
		Assert.DoesNotContain("checkedProduct", source, StringComparison.Ordinal);
		Assert.DoesNotContain("checkedIsPrototype", source, StringComparison.Ordinal);
	}

	[Fact]
	public void PrototypeStateIsVersionedAndMapReferencesAreMapOwned()
	{
		var keeper = Contract("runtime", "PrototypeKeeper.cs");
		var map = Contract("runtime", "PrototypeTerrainGrid.cs");
		var save = Contract("runtime", "PrototypeSaveData.cs");
		Assert.Contains("PrototypeService prototypeService", keeper, StringComparison.Ordinal);
		Assert.Contains("prototypeState", keeper, StringComparison.Ordinal);
		Assert.Contains("Scribe_Collections.Look(ref prototypes", map, StringComparison.Ordinal);
		Assert.Contains("PrototypeSaveSnapshot.CurrentSchemaVersion", save, StringComparison.Ordinal);
		Assert.DoesNotContain("Dictionary<Map, PrototypeTerrainGrid>", keeper, StringComparison.Ordinal);
	}

	[Fact]
	public void ProjectSwitchCancellationTargetsTrackedStableKeysNotEveryMatchingRecipe()
	{
		var keeper = Contract("runtime", "PrototypeKeeper.cs");
		Assert.Contains("prototypeService.CancelProject", keeper, StringComparison.Ordinal);
		Assert.Contains("keys.Contains(key.Value)", keeper, StringComparison.Ordinal);
		Assert.DoesNotContain("defsToCancel", keeper, StringComparison.Ordinal);
		Assert.DoesNotContain("AllGeneratedOpportunities.Where", keeper, StringComparison.Ordinal);
	}

	[Fact]
	public void SurgeryDiscoveryStartsFromAuthoritativeSpecificationsThenChecksThePatient()
	{
		var keeper = Contract("runtime", "PrototypeKeeper.cs");
		var listing = Contract("prototypes", "BillStack_DoListing_Patches.cs");
		Assert.Contains("OpportunityService.SpecificationsFor", keeper, StringComparison.Ordinal);
		Assert.Contains("PrototypeSurgery", keeper, StringComparison.Ordinal);
		Assert.Contains("patient.def.AllRecipes.Contains", keeper, StringComparison.Ordinal);
		Assert.Contains("AvailableReport", listing, StringComparison.Ordinal);
		Assert.Contains("GetPartsToApplyOn", listing, StringComparison.Ordinal);
		Assert.Contains("AvailableOnNow", listing, StringComparison.Ordinal);
	}

	[Fact]
	public void RemainingPrototypeTranspilersUseSemanticMatchingWithoutFixedLocalSlots()
	{
		var files = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "contracts"), "*.cs", SearchOption.AllDirectories);
		var source = string.Join("\n", files.Select(File.ReadAllText));
		Assert.DoesNotContain("OpCodes.Ldloc_0", source, StringComparison.Ordinal);
		Assert.DoesNotContain("OpCodes.Ldloc_1", source, StringComparison.Ordinal);
		Assert.DoesNotContain("Ldloc_S, (byte)", source, StringComparison.Ordinal);
		var constructionFailure = Contract("prototypes", "JobDriver_ConstructFinishFrame_MakeNewToils_Patches.cs");
		Assert.Contains("CodeMatcher", constructionFailure, StringComparison.Ordinal);
		Assert.Contains("instruction.IsLdloc()", Contract("compat", "DMM_Patch_BillStack_DoListing_Patches.cs"), StringComparison.Ordinal);
	}

	private static string Contract(string folder, string file) =>
		File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "contracts", folder, file));
}
