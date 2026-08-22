using PeteTimesSix.ResearchReinvented.Domain;
using PeteTimesSix.ResearchReinvented.Domain.Execution;
using PeteTimesSix.ResearchReinvented.Domain.State;
using PeteTimesSix.ResearchReinvented.Opportunities;
using Xunit;

namespace PeteTimesSix.ResearchReinvented.Tests;

public sealed class OpportunityActivityRegistryTests
{
	[Fact]
	public void SyntheticOptionalHandlerAdvancesServiceWithoutServiceChangesOrCentralSwitch()
	{
		var specification = Specification("CuratedFutureActivity");
		var service = Service(specification);
		var registry = new OpportunityActivityRegistry();
		var handlerId = new ActivityHandlerId("test.synthetic.optional");
		registry.Register(new TestHandler(handlerId, item => item.Spec.Type.DefName == "CuratedFutureActivity"));
		registry.RunStartupChecks();

		var result = registry.Query(handlerId, service, Project);

		Assert.Equal(ActivityQueryStatus.Available, result.Status);
		Assert.Single(result.Keys);
		Assert.Equal(7f, service.ApplyProgress(result.Keys[0], 7f));
		Assert.Equal(7f, service.ProgressFor(specification.Spec.Key));
	}

	[Fact]
	public void MissingDisabledAndFailedHandlersAreBoundedAndDoNotAffectGenericState()
	{
		var specification = Specification("Theory");
		var service = Service(specification);
		var registry = new OpportunityActivityRegistry();
		var disabledId = new ActivityHandlerId("test.disabled");
		var failedId = new ActivityHandlerId("test.failed");
		registry.Register(new TestHandler(disabledId, _ => true), enabled: false, disabledReason: "Optional integration is not active.");
		registry.Register(new TestHandler(failedId, _ => throw new InvalidOperationException("optional target changed")));

		var missing = registry.Query(new ActivityHandlerId("test.missing"), service, Project);
		var disabled = registry.Query(disabledId, service, Project);
		var failed = registry.Query(failedId, service, Project);
		var failedAgain = registry.Query(failedId, service, Project);

		Assert.Equal(ActivityQueryStatus.MissingHandler, missing.Status);
		Assert.Equal(ActivityQueryStatus.DisabledHandler, disabled.Status);
		Assert.Equal(ActivityQueryStatus.FailedHandler, failed.Status);
		Assert.Equal(ActivityQueryStatus.FailedHandler, failedAgain.Status);
		Assert.Equal(0f, service.ProgressFor(specification.Spec.Key));
		Assert.Single(service.SpecificationsFor(Project));
		Assert.Contains(registry.Diagnostics, item => item.HandlerId == failedId && item.Reason.Contains("optional target changed"));
	}

	[Fact]
	public void StartupSelfCheckDisablesOnlyTheUnavailableOptionalHandler()
	{
		var specification = Specification("Theory");
		var service = Service(specification);
		var registry = new OpportunityActivityRegistry();
		var optional = new ActivityHandlerId("test.optional");
		var generic = new ActivityHandlerId("test.generic");
		registry.Register(new TestHandler(optional, _ => true, () => ActivityHandlerSelfCheck.Unavailable("DLC is not active.")));
		registry.Register(new TestHandler(generic, _ => true));

		registry.RunStartupChecks();

		Assert.Equal(ActivityQueryStatus.DisabledHandler, registry.Query(optional, service, Project).Status);
		Assert.Equal(ActivityQueryStatus.Available, registry.Query(generic, service, Project).Status);
	}

	[Fact]
	public void HandlerSelectionAndRuntimeFilteringAreDeterministicAndDeduplicated()
	{
		var first = Specification("BType", "BSubject");
		var second = Specification("AType", "ASubject");
		var service = Service(first, second);
		var handlerId = new ActivityHandlerId("test.order");
		var registry = new OpportunityActivityRegistry();
		registry.Register(new RepeatingReverseHandler(handlerId));

		var result = registry.Query(handlerId, service, Project, item => item.Spec.Requirement.CanonicalSubject.DefName == "ASubject");

		Assert.Equal(ActivityQueryStatus.Available, result.Status);
		Assert.Equal(new[] { second.Spec.Key }, result.Keys);
	}

	[Fact]
	public void PerOwnerIndexRequiresExplicitInvalidationAndNeverLeaksAcrossOwners()
	{
		var index = new OpportunityQueryIndex<string, string, int>();
		var builds = 0;
		IEnumerable<int> Build() { builds++; return new[] { builds }; }

		Assert.Equal(new[] { 1 }, index.GetOrAdd("map-a", "thing-a", Build));
		Assert.Equal(new[] { 1 }, index.GetOrAdd("map-a", "thing-a", Build));
		Assert.Equal(new[] { 2 }, index.GetOrAdd("map-b", "thing-a", Build));
		index.Invalidate("map-a");
		Assert.Equal(new[] { 3 }, index.GetOrAdd("map-a", "thing-a", Build));
		Assert.Equal(new[] { 2 }, index.GetOrAdd("map-b", "thing-a", Build));
		index.InvalidateAll();
		Assert.Equal(new[] { 4 }, index.GetOrAdd("map-b", "thing-a", Build));
		Assert.Equal(2, index.Revision);
	}

	[Fact]
	public void DuplicateHandlerIdsAreRejectedInsteadOfSilentlyReplacingExecution()
	{
		var id = new ActivityHandlerId("test.duplicate");
		var registry = new OpportunityActivityRegistry();
		registry.Register(new TestHandler(id, _ => true));

		Assert.Throws<InvalidOperationException>(() => registry.Register(new TestHandler(id, _ => true)));
	}

	[Fact]
	public void EveryBuiltInMigratedActivityHasAUniqueStableHandlerId()
	{
		Assert.Equal(11, BuiltInActivityHandlerIds.All.Count);
		Assert.Equal(BuiltInActivityHandlerIds.All.Count, BuiltInActivityHandlerIds.All.Distinct().Count());
		Assert.All(BuiltInActivityHandlerIds.All, id => Assert.StartsWith("rr.", id.Value, StringComparison.Ordinal));
	}

	[Fact]
	public void OneActivityApplicationAdvancesExactlyOneDeterministicOpportunity()
	{
		var first = Specification("Analyse", "A");
		var second = Specification("Analyse", "B");
		var service = Service(second, first);
		var registry = new OpportunityActivityRegistry();
		registry.Register(new FirstOnlyHandler(BuiltInActivityHandlerIds.AnalysisBench));

		var result = registry.Query(BuiltInActivityHandlerIds.AnalysisBench, service, Project);
		var applied = service.ApplyProgress(result.Keys.Single(), 5f);

		Assert.Equal(5f, applied);
		Assert.Equal(5f, service.ProgressFor(first.Spec.Key));
		Assert.Equal(0f, service.ProgressFor(second.Spec.Key));
	}

	[Fact]
	public void SaveLoadDuringContinuousActivityResumesTheSameStableOpportunity()
	{
		var specification = Specification("Theory");
		var service = Service(specification);
		var registry = new OpportunityActivityRegistry();
		registry.Register(new TestHandler(BuiltInActivityHandlerIds.Theory, _ => true));
		var beforeSave = registry.Query(BuiltInActivityHandlerIds.Theory, service, Project).Keys.Single();
		service.ApplyProgress(beforeSave, 6f);

		var snapshot = service.CreateSnapshot(0);
		var restored = new OpportunityService();
		restored.Restore(snapshot);
		restored.SetSpecifications(Project, new[] { specification }, snapshot.CategoryBudgets);
		var afterLoad = registry.Query(BuiltInActivityHandlerIds.Theory, restored, Project).Keys.Single();
		restored.ApplyProgress(afterLoad, 4f);

		Assert.Equal(beforeSave, afterLoad);
		Assert.Equal(10f, restored.ProgressFor(specification.Spec.Key));
	}

	[Fact]
	public void ProjectSwitchInvalidationCannotReturnPriorProjectKeys()
	{
		var firstProjectSpec = Specification("Theory");
		var secondProject = new DefIdentity("ResearchProjectDef", "ProjectB");
		var secondSpec = new OpportunitySpecificationState(
			new OpportunitySpec(secondProject, new DefIdentity("ResearchOpportunityTypeDef", "Theory"), ResearchRelation.Direct,
				RequirementSpec.ForThing(new DefIdentity("ThingDef", "Subject")), 1f, false, false),
			new DefIdentity("ResearchOpportunityCategoryDef", "Theory"), 20f);
		var service = Service(firstProjectSpec);
		service.SetSpecifications(secondProject, new[] { secondSpec }, Array.Empty<SavedCategoryBudget>());
		var registry = new OpportunityActivityRegistry();
		registry.Register(new TestHandler(BuiltInActivityHandlerIds.Theory, _ => true));
		var index = new OpportunityQueryIndex<string, string, OpportunityKey>();
		var first = index.GetOrAdd("map", "theory", () => registry.Query(BuiltInActivityHandlerIds.Theory, service, Project).Keys);

		index.InvalidateAll();
		var second = index.GetOrAdd("map", "theory", () => registry.Query(BuiltInActivityHandlerIds.Theory, service, secondProject).Keys);

		Assert.Equal(new[] { firstProjectSpec.Spec.Key }, first);
		Assert.Equal(new[] { secondSpec.Spec.Key }, second);
		Assert.DoesNotContain(firstProjectSpec.Spec.Key, second);
	}

	[Fact]
	public void CentralProgressMathAppliesAllModifiersAndCapsAtRemainingOpportunityBudget()
	{
		Assert.Equal(30f, OpportunityProgressMath.CalculateAppliedAmount(
			baseAmount: 10f, currentProgress: 20f, maximumProgress: 100f,
			storytellerFactor: 1.5f, categoryFactor: 2f, researcherTechCostFactor: 1f,
			fastResearch: false));
		Assert.Equal(4f, OpportunityProgressMath.CalculateAppliedAmount(
			baseAmount: 10f, currentProgress: 96f, maximumProgress: 100f,
			storytellerFactor: 1.5f, categoryFactor: 2f, researcherTechCostFactor: 1f,
			fastResearch: false));
		Assert.Equal(100f, OpportunityProgressMath.CalculateAppliedAmount(
			baseAmount: 10f, currentProgress: 0f, maximumProgress: 100f,
			storytellerFactor: 1f, categoryFactor: 1f, researcherTechCostFactor: 2f,
			fastResearch: true));
		Assert.Equal(4f, OpportunityProgressMath.ClampMoteAmount(50f, 96f, 100f));
		Assert.Throws<ArgumentOutOfRangeException>(() => OpportunityProgressMath.CalculateAppliedAmount(1f, 0f, 10f, 1f, 1f, 0f, false));
	}

	private static readonly DefIdentity Project = new("ResearchProjectDef", "ProjectA");

	private static OpportunitySpecificationState Specification(string type, string subject = "Subject")
	{
		var spec = new OpportunitySpec(Project, new DefIdentity("ResearchOpportunityTypeDef", type), ResearchRelation.Direct,
			RequirementSpec.ForThing(new DefIdentity("ThingDef", subject)), 1f, false, false);
		return new OpportunitySpecificationState(spec, new DefIdentity("ResearchOpportunityCategoryDef", "Theory"), 20f);
	}

	private static OpportunityService Service(params OpportunitySpecificationState[] specifications)
	{
		var service = new OpportunityService();
		service.SetSpecifications(Project, specifications.Reverse(), Array.Empty<SavedCategoryBudget>());
		service.ActiveProject = Project;
		return service;
	}

	private sealed class TestHandler : IOpportunityActivityHandler
	{
		private readonly Func<OpportunitySpecificationState, bool> selector;
		private readonly Func<ActivityHandlerSelfCheck> startup;

		public TestHandler(ActivityHandlerId id, Func<OpportunitySpecificationState, bool> selector, Func<ActivityHandlerSelfCheck>? startup = null)
		{
			Id = id;
			this.selector = selector;
			this.startup = startup ?? ActivityHandlerSelfCheck.Ready;
		}

		public ActivityHandlerId Id { get; }
		public ActivityHandlerSelfCheck StartupCheck() => startup();
		public IEnumerable<OpportunityKey> Select(ActivityHandlerQuery query) => query.Specifications.Where(selector).Select(item => item.Spec.Key);
	}

	private sealed class RepeatingReverseHandler : IOpportunityActivityHandler
	{
		public RepeatingReverseHandler(ActivityHandlerId id) => Id = id;
		public ActivityHandlerId Id { get; }
		public ActivityHandlerSelfCheck StartupCheck() => ActivityHandlerSelfCheck.Ready();
		public IEnumerable<OpportunityKey> Select(ActivityHandlerQuery query) => query.Specifications.Reverse().SelectMany(item => new[] { item.Spec.Key, item.Spec.Key });
	}

	private sealed class FirstOnlyHandler : IOpportunityActivityHandler
	{
		public FirstOnlyHandler(ActivityHandlerId id) => Id = id;
		public ActivityHandlerId Id { get; }
		public ActivityHandlerSelfCheck StartupCheck() => ActivityHandlerSelfCheck.Ready();
		public IEnumerable<OpportunityKey> Select(ActivityHandlerQuery query) => query.Specifications.Take(1).Select(item => item.Spec.Key);
	}
}
