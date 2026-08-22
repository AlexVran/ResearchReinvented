#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using PeteTimesSix.ResearchReinvented.Domain;
using PeteTimesSix.ResearchReinvented.Domain.State;
using PeteTimesSix.ResearchReinvented.Opportunities;
using Verse;

namespace PeteTimesSix.ResearchReinvented.Rimworld
{
	internal sealed class DefIdentitySaveData : IExposable
	{
		private string? defType;
		private string? defName;

		public DefIdentitySaveData() { }
		internal DefIdentitySaveData(DefIdentity identity) { defType = identity.DefType; defName = identity.DefName; }
		internal DefIdentity? ToDomain() => string.IsNullOrWhiteSpace(defType) || string.IsNullOrWhiteSpace(defName) ? null : new DefIdentity(defType!, defName!);
		public void ExposeData() { Scribe_Values.Look(ref defType, "defType"); Scribe_Values.Look(ref defName, "defName"); }
	}

	internal sealed class SavedOpportunityStateData : IExposable
	{
		private string? key;
		private DefIdentitySaveData? project;
		private DefIdentitySaveData? type;
		private ResearchRelation relation;
		private RequirementKind requirementKind;
		private DefIdentitySaveData? subject;
		private DefIdentitySaveData? category;
		private float currentProgress;
		private float maximumProgress;
		private PreservedOpportunityStateKind stateKind;
		private List<DefIdentitySaveData>? alternates;
		private string? sourceId;

		public SavedOpportunityStateData() { }
		internal SavedOpportunityStateData(SavedOpportunityState state)
		{
			key = state.Key;
			project = Save(state.Project);
			type = Save(state.Type);
			relation = state.Relation;
			requirementKind = state.RequirementKind;
			subject = Save(state.CanonicalSubject);
			category = Save(state.Category);
			currentProgress = state.CurrentProgress;
			maximumProgress = state.MaximumProgress;
			stateKind = state.StateKind;
			alternates = state.AlternateSubjects.Select(identity => new DefIdentitySaveData(identity)).ToList();
			sourceId = state.SourceId;
		}

		internal SavedOpportunityState ToDomain() => new(
			key, project?.ToDomain(), type?.ToDomain(), relation, requirementKind, subject?.ToDomain(), category?.ToDomain(),
			currentProgress, maximumProgress, stateKind,
			(alternates ?? new List<DefIdentitySaveData>()).Select(item => item?.ToDomain()).Where(item => item != null)!, sourceId);

		public void ExposeData()
		{
			Scribe_Values.Look(ref key, "key");
			Scribe_Deep.Look(ref project, "project");
			Scribe_Deep.Look(ref type, "type");
			Scribe_Values.Look(ref relation, "relation", ResearchRelation.Direct);
			Scribe_Values.Look(ref requirementKind, "requirementKind", RequirementKind.None);
			Scribe_Deep.Look(ref subject, "subject");
			Scribe_Deep.Look(ref category, "category");
			Scribe_Values.Look(ref currentProgress, "currentProgress");
			Scribe_Values.Look(ref maximumProgress, "maximumProgress");
			Scribe_Values.Look(ref stateKind, "stateKind", PreservedOpportunityStateKind.Ordinary);
			Scribe_Collections.Look(ref alternates, "alternates", LookMode.Deep);
			Scribe_Values.Look(ref sourceId, "sourceId");
		}

		private static DefIdentitySaveData? Save(DefIdentity? identity) => identity == null ? null : new DefIdentitySaveData(identity);
	}

	internal sealed class SavedCategoryBudgetData : IExposable
	{
		private DefIdentitySaveData? project;
		private DefIdentitySaveData? category;
		private float budget;

		public SavedCategoryBudgetData() { }
		internal SavedCategoryBudgetData(SavedCategoryBudget value) { project = new DefIdentitySaveData(value.Project); category = new DefIdentitySaveData(value.Category); budget = value.Budget; }
		internal SavedCategoryBudget? ToDomain()
		{
			var projectIdentity = project?.ToDomain(); var categoryIdentity = category?.ToDomain();
			return projectIdentity == null || categoryIdentity == null || float.IsNaN(budget) || float.IsInfinity(budget) || budget < 0f ? null : new SavedCategoryBudget(projectIdentity, categoryIdentity, budget);
		}
		public void ExposeData() { Scribe_Deep.Look(ref project, "project"); Scribe_Deep.Look(ref category, "category"); Scribe_Values.Look(ref budget, "budget"); }
	}

	internal sealed class OpportunitySaveRootData : IExposable
	{
		private int schemaVersion = OpportunitySaveSnapshot.CurrentSchemaVersion;
		private DefIdentitySaveData? activeProject;
		private List<DefIdentitySaveData>? generatedProjects;
		private List<SavedOpportunityStateData>? progress;
		private List<SavedOpportunityStateData>? specialState;
		private List<SavedCategoryBudgetData>? categoryBudgets;
		private int settingsChangeTicker = -1;
		private bool requiresSpecificationRegeneration = true;

		public OpportunitySaveRootData() { }
		internal OpportunitySaveRootData(OpportunitySaveSnapshot snapshot)
		{
			schemaVersion = snapshot.SchemaVersion;
			activeProject = snapshot.ActiveProject == null ? null : new DefIdentitySaveData(snapshot.ActiveProject);
			generatedProjects = snapshot.GeneratedProjects.Select(item => new DefIdentitySaveData(item)).ToList();
			progress = snapshot.Progress.Select(item => new SavedOpportunityStateData(item)).ToList();
			specialState = snapshot.SpecialState.Select(item => new SavedOpportunityStateData(item)).ToList();
			categoryBudgets = snapshot.CategoryBudgets.Select(item => new SavedCategoryBudgetData(item)).ToList();
			settingsChangeTicker = snapshot.SettingsChangeTicker;
			requiresSpecificationRegeneration = snapshot.RequiresSpecificationRegeneration;
		}

		internal OpportunitySaveSnapshot ToDomain() => new(
			schemaVersion,
			activeProject?.ToDomain(),
			(generatedProjects ?? new List<DefIdentitySaveData>()).Select(item => item?.ToDomain()).Where(item => item != null)!,
			(progress ?? new List<SavedOpportunityStateData>()).Where(item => item != null).Select(item => item.ToDomain()),
			(specialState ?? new List<SavedOpportunityStateData>()).Where(item => item != null).Select(item => item.ToDomain()),
			(categoryBudgets ?? new List<SavedCategoryBudgetData>()).Where(item => item != null).Select(item => item.ToDomain()).Where(item => item != null)!,
			settingsChangeTicker,
			requiresSpecificationRegeneration);

		public void ExposeData()
		{
			Scribe_Values.Look(ref schemaVersion, "schemaVersion", OpportunitySaveSnapshot.CurrentSchemaVersion, forceSave: true);
			Scribe_Deep.Look(ref activeProject, "activeProject");
			Scribe_Collections.Look(ref generatedProjects, "generatedProjects", LookMode.Deep);
			Scribe_Collections.Look(ref progress, "progress", LookMode.Deep);
			Scribe_Collections.Look(ref specialState, "specialState", LookMode.Deep);
			Scribe_Collections.Look(ref categoryBudgets, "categoryBudgets", LookMode.Deep);
			Scribe_Values.Look(ref settingsChangeTicker, "settingsChangeTicker", -1);
			Scribe_Values.Look(ref requiresSpecificationRegeneration, "requiresSpecificationRegeneration", true, forceSave: true);
		}
	}
}
