using PeteTimesSix.ResearchReinvented.Domain;
using PeteTimesSix.ResearchReinvented.Domain.Presentation;
using PeteTimesSix.ResearchReinvented.Domain.Settings;
using PeteTimesSix.ResearchReinvented.Domain.State;
using PeteTimesSix.ResearchReinvented.Opportunities;
using Xunit;

namespace PeteTimesSix.ResearchReinvented.Tests;

public sealed class OpportunityPresentationTests
{
	[Fact]
	public void ReadModelIsDetachedOrderedAndIncludesServiceTotalsAndReasons()
	{
		var reason = new GenerationReason(GenerationReasonKind.Unlock, EvidenceSource.ThingDefinition, Thing("SourceBench"), "direct unlock");
		var second = Specification("ThingB", 30f);
		var first = Specification("ThingA", 20f, reason);
		var service = new OpportunityService();
		service.SetSpecifications(Project, new[] { second, first }, new[] { new SavedCategoryBudget(Project, Category, 80f) });
		service.ApplyProgress(second.Spec.Key, 5f);
		service.ApplyProgress(first.Spec.Key, 7f);

		var model = service.ReadModelFor(Project);

		var category = Assert.Single(model.Categories);
		Assert.Equal(12f, category.Progress);
		Assert.Equal(80f, category.Budget);
		Assert.True(category.HasBudget);
		Assert.Equal(new[] { first.Spec.Key, second.Spec.Key }, category.Opportunities.Select(item => item.Key));
		Assert.Equal(7f, category.Opportunities[0].Progress);
		Assert.Equal(20f, category.Opportunities[0].MaximumProgress);
		Assert.True(category.Opportunities[0].Rare);
		Assert.False(category.Opportunities[0].Freebie);
		Assert.Equal(reason, Assert.Single(category.Opportunities[0].Reasons));

		service.ApplyProgress(first.Spec.Key, 4f);
		Assert.Equal(7f, category.Opportunities[0].Progress);
		Assert.Equal(11f, service.ReadModelFor(Project).Categories.Single().Opportunities[0].Progress);
	}

	[Fact]
	public void OrphanCreditAppearsInCategoryReadModelWithoutInventingAnOpportunityRow()
	{
		var removed = SavedOpportunityState.FromSpecification(Specification("RemovedThing", 20f), 9f);
		var service = new OpportunityService();
		service.RestoreLegacy(new[] { new LegacyOpportunityMigrationDto(removed) }, new[] { new SavedCategoryBudget(Project, Category, 50f) }, Project, new[] { Project }, 0);
		service.SetSpecifications(Project, Array.Empty<OpportunitySpecificationState>(), new[] { new SavedCategoryBudget(Project, Category, 50f) });

		var category = Assert.Single(service.ReadModelFor(Project).Categories);
		Assert.Equal(9f, category.Progress);
		Assert.Equal(50f, category.Budget);
		Assert.True(category.HasBudget);
		Assert.Empty(category.Opportunities);
	}

	[Fact]
	public void SettingsResolverAppliesOnlySparseOverrides()
	{
		var preset = Settings(importanceMultiplier: 1f, speed: 1f);
		var changed = Settings(importanceMultiplier: 2f, speed: 1f);

		var difference = CategorySettingsResolver.SparseDifference(preset, changed);
		var resolved = CategorySettingsResolver.Resolve(preset, difference);

		Assert.Equal(2f, difference.ImportanceMultiplier);
		Assert.Null(difference.ResearchSpeedMultiplier);
		Assert.Equal(2f, resolved.ImportanceMultiplier);
		Assert.Equal(1f, resolved.ResearchSpeedMultiplier);
	}

	[Fact]
	public void PresetSwitchRetainsExplicitOverrideAndAdoptsUnchangedValues()
	{
		var oldPreset = Settings(importanceMultiplier: 1f, speed: 1f);
		var edited = Settings(importanceMultiplier: 1.5f, speed: 1f);
		var sparse = CategorySettingsResolver.SparseDifference(oldPreset, edited);
		var newPreset = Settings(importanceMultiplier: 0.5f, speed: 2f);

		var resolved = CategorySettingsResolver.Resolve(newPreset, sparse);

		Assert.Equal(1.5f, resolved.ImportanceMultiplier);
		Assert.Equal(2f, resolved.ResearchSpeedMultiplier);
	}

	[Fact]
	public void ReturningToPresetProducesNoSavedOverride()
	{
		var preset = Settings(importanceMultiplier: 1.25f, speed: 0.75f);
		var sparse = CategorySettingsResolver.SparseDifference(preset, preset);
		Assert.True(sparse.IsEmpty);
	}

	[Fact]
	public void PresetSwitchClampsCountedWeightToExplicitLowerMultiplier()
	{
		var preset = Settings(importanceMultiplier: 2f, speed: 1f);
		var changes = new CategorySettingsOverride(importanceMultiplier: 0.5f);
		var resolved = CategorySettingsResolver.Resolve(preset, changes);

		Assert.Equal(0.5f, resolved.ImportanceMultiplier);
		Assert.Equal(0.5f, resolved.ImportanceMultiplierCounted);
	}

	[Fact]
	public void SettingsReconciliationPreservesMatchingProgressAndRestoresRemovedKeys()
	{
		var kept = Specification("ThingA", 20f);
		var temporarilyRemoved = Specification("ThingB", 30f);
		var service = new OpportunityService();
		service.SetSpecifications(Project, new[] { kept, temporarilyRemoved }, new[] { new SavedCategoryBudget(Project, Category, 100f) });
		service.ApplyProgress(kept.Spec.Key, 7f);
		service.ApplyProgress(temporarilyRemoved.Spec.Key, 9f);

		var resized = Specification("ThingA", 45f);
		service.SetSpecifications(Project, new[] { resized }, new[] { new SavedCategoryBudget(Project, Category, 80f) });

		Assert.Equal(7f, service.ProgressFor(resized.Spec.Key));
		Assert.Equal(16f, service.ReadModelFor(Project).Categories.Single().Progress);
		Assert.Single(service.Orphans);
		Assert.Equal(80f, service.CreateSnapshot(1).CategoryBudgets.Single().Budget);

		service.SetSpecifications(Project, new[] { temporarilyRemoved, resized }, new[] { new SavedCategoryBudget(Project, Category, 120f) });

		Assert.Equal(7f, service.ProgressFor(resized.Spec.Key));
		Assert.Equal(9f, service.ProgressFor(temporarilyRemoved.Spec.Key));
		Assert.Empty(service.Orphans);
		Assert.Equal(16f, service.ReadModelFor(Project).Categories.Single().Progress);
		Assert.Equal(120f, service.CreateSnapshot(2).CategoryBudgets.Single().Budget);
	}

	private static OpportunitySpecificationState Specification(string subject, float maximum, params GenerationReason[] reasons)
	{
		var spec = new OpportunitySpec(Project, Type, ResearchRelation.Direct, RequirementSpec.ForThing(Thing(subject)), 1f, rare: true, freebie: false, reasons);
		return new OpportunitySpecificationState(spec, Category, maximum);
	}

	private static CategorySettingsValue Settings(float importanceMultiplier, float speed) => new(
		enabled: true,
		importanceStatic: 0f,
		importanceMultiplier,
		importanceMultiplierCounted: Math.Min(importanceMultiplier, 1f),
		infiniteOverflow: false,
		targetIterations: 3f,
		researchSpeedMultiplier: speed,
		availableAtOverallProgress: new ProgressRange(0f, 1f));

	private static readonly DefIdentity Project = new("ResearchProjectDef", "ProjectA");
	private static readonly DefIdentity Type = new("ResearchOpportunityTypeDef", "Analyse");
	private static readonly DefIdentity Category = new("ResearchOpportunityCategoryDef", "Material");
	private static DefIdentity Thing(string name) => new("ThingDef", name);
}
