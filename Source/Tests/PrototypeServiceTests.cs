using PeteTimesSix.ResearchReinvented.Domain;
using PeteTimesSix.ResearchReinvented.Domain.Prototypes;
using PeteTimesSix.ResearchReinvented.Domain.State;
using PeteTimesSix.ResearchReinvented.Opportunities;
using Xunit;

namespace PeteTimesSix.ResearchReinvented.Tests;

public sealed class PrototypeServiceTests
{
	[Fact]
	public void StableKeysAreDeterministicAndSeparateMapsAndArtifactKinds()
	{
		var first = Key("map-4", PrototypeArtifactKind.Frame, "Thing_91");
		Assert.Equal(first.Value, Key("map-4", PrototypeArtifactKind.Frame, "Thing_91").Value);
		Assert.NotEqual(first.Value, Key("map-5", PrototypeArtifactKind.Frame, "Thing_91").Value);
		Assert.NotEqual(first.Value, Key("map-4", PrototypeArtifactKind.CompletedProduct, "Thing_91").Value);
	}

	[Fact]
	public void BlueprintFrameUnfinishedProductTerrainAndSurgeryTransitionsAreTracked()
	{
		var service = new PrototypeService();
		var blueprint = Key("map-1", PrototypeArtifactKind.Blueprint, "blueprint-1");
		Assert.True(service.Register(Record(blueprint, PrototypeArtifactKind.Blueprint)));
		var frame = Key("map-1", PrototypeArtifactKind.Frame, "frame-2");
		Assert.True(service.Transition(blueprint, frame, PrototypeArtifactKind.Frame, PrototypeLifecycleState.Active));
		var product = Key("map-1", PrototypeArtifactKind.CompletedProduct, "product-3");
		Assert.True(service.Transition(frame, product, PrototypeArtifactKind.CompletedProduct, PrototypeLifecycleState.Completed));

		var unfinished = Key("map-1", PrototypeArtifactKind.UnfinishedItem, "unfinished-4");
		Assert.True(service.Register(Record(unfinished, PrototypeArtifactKind.UnfinishedItem)));
		Assert.True(service.Finish(unfinished, PrototypeLifecycleState.Failed));
		var terrain = Key("map-1", PrototypeArtifactKind.Terrain, "cell-7-9");
		var foundation = Key("map-1", PrototypeArtifactKind.FoundationTerrain, "cell-7-9");
		var surgery = Key("map-1", PrototypeArtifactKind.SurgeryBill, "bill-8");
		var bill = Key("map-1", PrototypeArtifactKind.Bill, "bill-9");
		Assert.True(service.Register(Record(terrain, PrototypeArtifactKind.Terrain, PrototypeLifecycleState.Completed)));
		Assert.True(service.Register(Record(foundation, PrototypeArtifactKind.FoundationTerrain, PrototypeLifecycleState.Completed)));
		Assert.True(service.Register(Record(surgery, PrototypeArtifactKind.SurgeryBill)));
		Assert.True(service.Register(Record(bill, PrototypeArtifactKind.Bill)));
		Assert.True(service.Finish(surgery, PrototypeLifecycleState.Completed));

		Assert.Equal(6, service.Records.Count);
		Assert.Contains(service.Records, item => item.Kind == PrototypeArtifactKind.CompletedProduct && item.State == PrototypeLifecycleState.Completed);
		Assert.Contains(service.Records, item => item.Kind == PrototypeArtifactKind.UnfinishedItem && item.State == PrototypeLifecycleState.Failed);
		Assert.Contains(service.Records, item => item.Kind == PrototypeArtifactKind.Terrain);
		Assert.Contains(service.Records, item => item.Kind == PrototypeArtifactKind.FoundationTerrain);
		Assert.Contains(service.Records, item => item.Kind == PrototypeArtifactKind.SurgeryBill && item.State == PrototypeLifecycleState.Completed);
		Assert.Contains(service.Records, item => item.Kind == PrototypeArtifactKind.Bill && item.State == PrototypeLifecycleState.Active);
	}

	[Fact]
	public void NewGameRoundTripPreservesStableStateAndOrdering()
	{
		var service = new PrototypeService();
		service.Register(Record(Key("map-b", PrototypeArtifactKind.Frame, "2"), PrototypeArtifactKind.Frame));
		service.Register(Record(Key("map-a", PrototypeArtifactKind.UnfinishedItem, "1"), PrototypeArtifactKind.UnfinishedItem));

		var saved = service.CreateSnapshot();
		var restored = new PrototypeService();
		restored.Restore(saved);
		Assert.Equal(saved.Records.Select(item => item.Key), restored.CreateSnapshot().Records.Select(item => item.Key));
		Assert.All(restored.Records, item => Assert.Equal(PrototypeLifecycleState.Active, item.State));
	}

	[Fact]
	public void ProjectSwitchCancelsOnlyActiveArtifactsForThePreviousProject()
	{
		var service = new PrototypeService();
		var activeOld = Record(Key("map", PrototypeArtifactKind.Frame, "old-active", "OldProject"), PrototypeArtifactKind.Frame, project: "OldProject");
		var completedOld = Record(Key("map", PrototypeArtifactKind.CompletedProduct, "old-done", "OldProject"), PrototypeArtifactKind.CompletedProduct, PrototypeLifecycleState.Completed, "OldProject");
		var activeNew = Record(Key("map", PrototypeArtifactKind.Frame, "new-active", "NewProject"), PrototypeArtifactKind.Frame, project: "NewProject");
		service.Register(activeOld);
		service.Register(completedOld);
		service.Register(activeNew);

		var cancelled = service.CancelProject(Project("OldProject"));
		Assert.Single(cancelled);
		Assert.Equal(activeOld.Key, cancelled[0].Key);
		Assert.Equal(PrototypeLifecycleState.Cancelled, service.Records.Single(item => item.Key.Equals(activeOld.Key)).State);
		Assert.Equal(PrototypeLifecycleState.Completed, service.Records.Single(item => item.Key.Equals(completedOld.Key)).State);
		Assert.True(service.IsActive(activeNew.Key));
	}

	[Fact]
	public void FailedOrCancelledArtifactsStayTerminalAcrossSaveLoad()
	{
		var service = new PrototypeService();
		var failed = Key("map", PrototypeArtifactKind.Frame, "failed");
		var cancelled = Key("map", PrototypeArtifactKind.SurgeryBill, "cancelled");
		service.Register(Record(failed, PrototypeArtifactKind.Frame));
		service.Register(Record(cancelled, PrototypeArtifactKind.SurgeryBill));
		service.Finish(failed, PrototypeLifecycleState.Failed);
		service.Finish(cancelled, PrototypeLifecycleState.Cancelled);

		var restored = new PrototypeService();
		restored.Restore(service.CreateSnapshot());
		Assert.Equal(PrototypeLifecycleState.Failed, restored.Records.Single(item => item.Key.Equals(failed)).State);
		Assert.Equal(PrototypeLifecycleState.Cancelled, restored.Records.Single(item => item.Key.Equals(cancelled)).State);
	}

	[Fact]
	public void CompletedArtifactIsNotDowngradedByLaterCleanupCancellation()
	{
		var service = new PrototypeService();
		var key = Key("map", PrototypeArtifactKind.SurgeryBill, "completed-then-deleted");
		service.Register(Record(key, PrototypeArtifactKind.SurgeryBill));
		service.Finish(key, PrototypeLifecycleState.Completed);
		service.Finish(key, PrototypeLifecycleState.Cancelled);
		Assert.Equal(PrototypeLifecycleState.Completed, service.Records.Single().State);
	}

	[Fact]
	public void DuplicateSemanticStateMergesDeterministicallyRegardlessOfInputOrder()
	{
		var key = Key("map", PrototypeArtifactKind.Frame, "same");
		var active = Saved(key, PrototypeLifecycleState.Active, "z");
		var completed = Saved(key, PrototypeLifecycleState.Completed, "a");
		var forward = Restore(active, completed);
		var reverse = Restore(completed, active);
		Assert.Equal(PrototypeLifecycleState.Completed, forward.Records.Single().State);
		Assert.Equal(forward.CreateSnapshot().Records.Single().State, reverse.CreateSnapshot().Records.Single().State);
		Assert.Contains(forward.Diagnostics, item => item.Kind == "DuplicateMerged");
	}

	[Fact]
	public void MalformedRecordsAreOrphanedWithoutActivatingArtifacts()
	{
		var malformed = new PrototypeSavedRecord("not-a-key", Project("ProjectA"), PrototypeArtifactKind.Frame, PrototypeLifecycleState.Active);
		var malformedEnum = new PrototypeSavedRecord(Key("map", PrototypeArtifactKind.Frame, "unknown-enum").Value, Project("ProjectA"), (PrototypeArtifactKind)999, PrototypeLifecycleState.Active);
		var service = Restore(malformed, malformedEnum);
		Assert.Empty(service.Records);
		Assert.Equal(2, service.Orphans.Count);
		Assert.Contains(service.Diagnostics, item => item.Kind == "MalformedRecord");
		Assert.Equal(2, service.CreateSnapshot().Records.Count);
	}

	[Fact]
	public void FutureSchemaIsPreservedReadOnlyWithoutDisablingOrdinarySystems()
	{
		var record = Saved(Key("map", PrototypeArtifactKind.Frame, "future"), PrototypeLifecycleState.Active);
		var future = new PrototypeSaveSnapshot(99, new[] { record });
		var service = new PrototypeService();
		service.Restore(future);
		Assert.True(service.IsReadOnly);
		Assert.False(service.Register(Record(Key("map", PrototypeArtifactKind.Frame, "new"), PrototypeArtifactKind.Frame)));
		Assert.Same(future, service.CreateSnapshot());
		Assert.True(service.Enabled);
	}

	[Fact]
	public void OldMalformedSchemaSalvagesOnlyWellFormedRecords()
	{
		var valid = Saved(Key("map", PrototypeArtifactKind.Frame, "salvaged"), PrototypeLifecycleState.Active);
		var malformed = new PrototypeSavedRecord("broken", Project("ProjectA"), PrototypeArtifactKind.Frame, PrototypeLifecycleState.Active);
		var service = new PrototypeService();
		service.Restore(new PrototypeSaveSnapshot(0, new[] { malformed, valid }));
		Assert.Single(service.Records);
		Assert.Single(service.Orphans);
		Assert.Contains(service.Diagnostics, item => item.Kind == "MalformedSchema");
	}

	[Fact]
	public void MigrationRoundTripIsIdempotent()
	{
		var first = Restore(
			Saved(Key("map", PrototypeArtifactKind.Frame, "one"), PrototypeLifecycleState.Active),
			Saved(Key("map", PrototypeArtifactKind.CompletedProduct, "two"), PrototypeLifecycleState.Completed));
		var once = first.CreateSnapshot();
		var second = new PrototypeService();
		second.Restore(once);
		var twice = second.CreateSnapshot();
		Assert.Equal(once.SchemaVersion, twice.SchemaVersion);
		Assert.Equal(once.Records.Select(Tuple), twice.Records.Select(Tuple));
	}

	[Fact]
	public void FeatureFailureWarnsOnceAndPrototypeDisableDoesNotMutateState()
	{
		var service = new PrototypeService();
		var key = Key("map", PrototypeArtifactKind.Frame, "one");
		service.Register(Record(key, PrototypeArtifactKind.Frame));
		Assert.True(service.DisableFeature("construction-completion", "Completion hook unavailable; construction prototype credit is disabled."));
		Assert.False(service.DisableFeature("construction-completion", "duplicate"));
		Assert.Single(service.Diagnostics, item => item.Kind == "FeatureDisabled");
		service.SetEnabled(false);
		Assert.False(service.IsActive(key));
		Assert.False(service.Finish(key, PrototypeLifecycleState.Completed));
		Assert.Single(service.Records);
	}

	[Fact]
	public void OrdinaryOpportunityProgressContinuesWhenPrototypingIsDisabled()
	{
		var prototypes = new PrototypeService();
		prototypes.SetEnabled(false);
		var specification = new OpportunitySpecificationState(
			new OpportunitySpec(Project("ProjectA"), new DefIdentity("ResearchOpportunityTypeDef", "BasicResearch"), ResearchRelation.Direct, RequirementSpec.Nothing(), 1f, false, false),
			new DefIdentity("ResearchOpportunityCategoryDef", "Theory"), 20f);
		var opportunities = new OpportunityService();
		opportunities.SetSpecifications(Project("ProjectA"), new[] { specification }, Array.Empty<SavedCategoryBudget>());

		Assert.Equal(4f, opportunities.ApplyProgress(specification.Spec.Key, 4f));
		Assert.False(prototypes.Enabled);
	}

	private static PrototypeService Restore(params PrototypeSavedRecord[] records)
	{
		var service = new PrototypeService();
		service.Restore(new PrototypeSaveSnapshot(PrototypeSaveSnapshot.CurrentSchemaVersion, records));
		return service;
	}

	private static PrototypeSavedRecord Saved(PrototypeArtifactKey key, PrototypeLifecycleState state, string? subject = null) =>
		new(key.Value, Project("ProjectA"), PrototypeArtifactKind.Frame, state, Opportunity().Value, subject);
	private static (string? Key, PrototypeLifecycleState State, string? Opportunity, string? Subject) Tuple(PrototypeSavedRecord item) =>
		(item.Key, item.State, item.OpportunityKey, item.Subject);
	private static PrototypeRecord Record(PrototypeArtifactKey key, PrototypeArtifactKind kind, PrototypeLifecycleState state = PrototypeLifecycleState.Active, string project = "ProjectA") =>
		new(key, Project(project), kind, state, Opportunity(), "subject");
	private static PrototypeArtifactKey Key(string map, PrototypeArtifactKind kind, string artifact, string project = "ProjectA") =>
		PrototypeArtifactKey.Create(Project(project), map, kind, artifact);
	private static OpportunityKey Opportunity() => OpportunityKey.Create(Project("ProjectA"), new DefIdentity("ResearchOpportunityTypeDef", "PrototypeConstruction"), ResearchRelation.Direct, RequirementSpec.ForThing(new DefIdentity("ThingDef", "PrototypeThing")));
	private static DefIdentity Project(string name) => new("ResearchProjectDef", name);
}
