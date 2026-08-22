using System.Collections.Generic;
using PeteTimesSix.ResearchReinvented.Defs;
using PeteTimesSix.ResearchReinvented.Domain.DefIndex;
using PeteTimesSix.ResearchReinvented.Opportunities;
using RimWorld;
using Verse;

namespace PeteTimesSix.ResearchReinvented.Rimworld.DefModExtensions
{
	/// <summary>
	/// Optional author metadata for Phase 5 opportunity inference. It is captured
	/// into immutable snapshots at startup; rules never retain live Def objects.
	/// </summary>
	public sealed class ResearchOpportunityOverrides : DefModExtension
	{
		public List<ResearchOpportunityOverride> overrides = new List<ResearchOpportunityOverride>();
	}

	public sealed class ResearchOpportunityOverride
	{
		public OpportunityOverrideAction action;
		public ResearchProjectDef project;
		public ResearchOpportunityTypeDef opportunityType;
		public Def subject;
		public ResearchRelation? relation;
		public ResearchOpportunityTypeDef replacementOpportunityType;
		public Def replacementSubject;
		public float importanceMultiplier = 1f;
	}
}
