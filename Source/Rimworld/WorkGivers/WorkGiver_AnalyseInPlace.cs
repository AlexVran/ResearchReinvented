using PeteTimesSix.ResearchReinvented.DefOfs;
using PeteTimesSix.ResearchReinvented.Defs;
using PeteTimesSix.ResearchReinvented.Extensions;
using PeteTimesSix.ResearchReinvented.Managers;
using PeteTimesSix.ResearchReinvented.Opportunities;
using PeteTimesSix.ResearchReinvented.OpportunityComps;
using PeteTimesSix.ResearchReinvented.Rimworld;
using PeteTimesSix.ResearchReinvented.Rimworld.JobDrivers;
using PeteTimesSix.ResearchReinvented.Rimworld.MiscData;
using PeteTimesSix.ResearchReinvented.Utilities;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;

namespace PeteTimesSix.ResearchReinvented.Rimworld.WorkGivers
{

	public class WorkGiver_AnalyseInPlace : WorkGiver_Scanner
	{
		public override bool Prioritized => true;

		public static Type DriverClass = typeof(JobDriver_AnalyseInPlace);

		private static IEnumerable<ResearchOpportunity> MatchingOpportunities => ResearchOpportunityManager.Instance.Execution
			.QueryCurrent(ActivityHandlerIds.AnalysisFieldThing);
        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            if (Find.ResearchManager.GetProject() == null)
                return Enumerable.Empty<Thing>();

            return ThingsForMap(pawn.MapHeld);
        }

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
		{
			ResearchProjectDef currentProj = Find.ResearchManager.GetProject();
			if (currentProj == null)
				return true;

			return !MatchingOpportunities.Any(o => !o.IsFinished);
		}

		public override bool HasJobOnThing(Pawn pawn, Thing thing, bool forced = false)
		{
            // Dont actually do this. We want minified things to be examined at benches.
            //var unminifiedThing = thing.GetInnerIfMinified();
            //var thingDef = unminifiedThing.def;

            if (thing.IsForbidden(pawn))
                return false;

            if (!pawn.CanReserve(thing, 1, -1, null, forced))
                return false;

            var opportunity = FilterCacheFor(thing, pawn).FirstOrDefault();
			if (opportunity == null)
				return false;
            if (PrototypeKeeper.Instance.IsPrototype(thing) && opportunity.relation != ResearchRelation.Ancestor)
            {
                JobFailReason.Is(StringsCache.JobFail_IsPrototype, null);
                return false;
            }

            if (Find.ResearchManager.GetProject().HasAnyPrerequisites() && !FieldResearchHelper.GetValidResearchKits(pawn, Find.ResearchManager.GetProject()).Any())
            {
                JobFailReason.Is(StringsCache.JobFail_NeedResearchKit, null);
                return false;
            }

            if (thing.def.hasInteractionCell)
            {
                if (!pawn.CanReserveSittableOrSpot(thing.InteractionCell, forced))
                    return false;
            }
            else
            {
                var reachable = AdjacencyHelper.GenReachableAdjacentCells(thing, pawn, true);
                if (!reachable.Any())
                    return false;
            }

            return new HistoryEvent(HistoryEventDefOf.Researching, pawn.Named(HistoryEventArgsNames.Doer)).Notify_PawnAboutToDo_Job();
        }

        public override Job JobOnThing(Pawn pawn, Thing thing, bool forced = false)
		{
			var opportunity = FilterCacheFor(thing, pawn).First();

			var jobDef = opportunity.JobDefs.First(j => j.driverClass == DriverClass);
			Job job = JobMaker.MakeJob(jobDef, thing, expiryInterval: 1500, checkOverrideOnExpiry: true);
			//ResearchOpportunityManager.instance.AssociateJobWithOpportunity(pawn, job, opportunity);
			return job;
		}

		//cache is built once per tick, to avoid working on already finished opportunities or opportunities from a different project
		private static int cacheBuiltOnTick = -1;
		private static int cacheBuiltForExecutionRevision = -1;
        public static Dictionary<Map, List<Thing>> _things = new Dictionary<Map, List<Thing>>();

        public static List<Thing> ThingsForMap(Map map)
        {
            if (cacheBuiltOnTick != Find.TickManager.TicksAbs || cacheBuiltForExecutionRevision != ResearchOpportunityManager.Instance.Execution.MapIndexRevision)
            {
                BuildCache();
            }
            return map != null && _things.TryGetValue(map, out var things) ? things : new List<Thing>();
        }

        public static void BuildCache()
		{
            _things.Clear();

            if (Find.ResearchManager.GetProject() == null)
				return;

            foreach (var map in ResearchRuntimeServices.Current.Maps)
            {
                var list = new List<Thing>();

                _things[map] = list;
                var added = new HashSet<Thing>();
                foreach (var opportunity in ResearchOpportunityManager.Instance.Execution.QueryCurrent(ActivityHandlerIds.AnalysisFieldThing, OpportunityAvailability.Available))
                {
                    if (!(opportunity.requirement is ROComp_RequiresThing requiresThing) || requiresThing.AllThings == null)
                        continue;
                    foreach (var thingDef in requiresThing.AllThings)
                    foreach (var thing in map.listerThings.ThingsOfDef(thingDef))
                    {
                        if (added.Add(thing) && thing.FactionAllowsAnalysis())
                            list.Add(thing);
                    }
                }
            }

            cacheBuiltOnTick = Find.TickManager.TicksAbs;
			cacheBuiltForExecutionRevision = ResearchOpportunityManager.Instance.Execution.MapIndexRevision;
		}

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static IReadOnlyList<ResearchOpportunity> FilterCacheFor(Thing thing, Pawn pawn)
        {
            return ResearchOpportunityManager.Instance.Execution.QueryForMap(pawn.MapHeld, ActivityHandlerIds.AnalysisFieldThing, thing.def);
        }
    }
}
