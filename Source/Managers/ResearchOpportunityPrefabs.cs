using PeteTimesSix.ResearchReinvented.Defs;
using PeteTimesSix.ResearchReinvented.Managers.OpportunityFactories;
using PeteTimesSix.ResearchReinvented.Opportunities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace PeteTimesSix.ResearchReinvented.Managers
{
    public static class ResearchOpportunityPrefabs
    {
        public static readonly float MIN_RESEARCH_POINTS = LegacyOpportunitySemantics.MinimumResearchPoints;

        //public static Dictionary<ResearchProjectDef, List<ResearchOpportunity>> Opportunities { get; private set; }

        /*public static void GenerateAllImplicitOpportunities()
        {
            if (Opportunities != null)
                return;

            Opportunities = new Dictionary<ResearchProjectDef, List<ResearchOpportunity>>();

            foreach (var project in DefDatabase<ResearchProjectDef>.AllDefsListForReading)
            {
                if (!Opportunities.ContainsKey(project))
                    Opportunities[project] = new List<ResearchOpportunity>();

                var opportunities = MakeOpportunitiesForProject(project);
                Opportunities[project] = opportunities;
            }
        }*/

        public static (List<ResearchOpportunity> opportunities, List<ResearchOpportunityCategoryTotalsStore> categoryStores) MakeOpportunitiesForProject(ResearchProjectDef project)
        {
            if (ResearchReinvented_Debug.debugPrintouts) 
                Log.Message($"Generating opportunities for project {project.label}...");

            var projectOpportunities = new List<ResearchOpportunity>();

            var factory = new MasterFactory();
            projectOpportunities.AddRange(factory.GenerateOpportunities(project));

            HashSet<ResearchOpportunityCategoryDef> categories = new HashSet<ResearchOpportunityCategoryDef>();
            foreach (var opportunity in projectOpportunities)
            {
                foreach (var category in opportunity.def.GetAllCategories())
                {
                    if (!categories.Contains(category))
                        categories.Add(category);
                }
            }

            float projectResearchPoints = project.baseCost;
            float totalMultiplier = 0;

            foreach (var category in categories)
            {
                var anyOpportunities = projectOpportunities.Any(o => o.def.GetCategory(o.relation) == category/* && o.relation == relation*/);
                if (anyOpportunities)
                {
                    totalMultiplier += category.Settings.importanceMultiplierCounted;
                }
            }
            totalMultiplier = LegacyOpportunitySemantics.NormalizeTotalMultiplier(totalMultiplier);

            List<ResearchOpportunityCategoryTotalsStore> totalStores = new List<ResearchOpportunityCategoryTotalsStore>();

            foreach (var category in categories)
            {
                var allMatchingOpportunities = projectOpportunities.Where(o => o.def.GetCategory(o.relation) == category);
                var countedMatchingOpportunities = allMatchingOpportunities.Where(o => !(o.IsRare) && !(o.IsFreebie));
                //var matchingOpportunities = projectOpportunities.Where(o => o.def.GetCategory(o.relation) == category);

                var totalsStore = new ResearchOpportunityCategoryTotalsStore() { project = project, category = category };
                totalsStore.researchPoints = LegacyOpportunitySemantics.CalculateCategoryBudget(
                    projectResearchPoints,
                    totalMultiplier,
                    category.Settings.importanceMultiplier,
                    category.Settings.importanceStatic);

                totalStores.Add(totalsStore);

                if (!allMatchingOpportunities.Any())
                    continue;

                var categoryImportanceTotal = countedMatchingOpportunities.Sum(o => o.importance);
                var matchingOpportunityTypes = allMatchingOpportunities.Select(o => (def: o.def, rel: o.relation)).ToHashSet();

                var minimumOpportunityResearchPoints = totalsStore.researchPoints / category.Settings.targetIterations;

                foreach (var type in matchingOpportunityTypes)
                {
                    float typeResearchPoints = totalsStore.researchPoints / matchingOpportunityTypes.Count();

                    var allMatchingOpportunitiesOfType = allMatchingOpportunities.Where(o => o.def == type.def && o.relation == type.rel);
                    var countedMatchingOpportunitiesOfType = countedMatchingOpportunities.Where(o => o.def == type.def && o.relation == type.rel);
                    var matchCount = countedMatchingOpportunitiesOfType.Count();       //attempt to make rares as valuable for research as all other options combined
                    float typeImportanceTotal = countedMatchingOpportunitiesOfType.Sum(o => o.requirement.IsRare ? matchCount : o.importance);
                    float baseImportance = LegacyOpportunitySemantics.CalculateBaseImportance(
                        typeImportanceTotal,
                        category.Settings.targetIterations);

                    if (ResearchReinvented_Debug.debugPrintouts)
                        Log.Message($"project {project} ({projectResearchPoints}) category {category.label} ({categoryImportanceTotal}) min: {minimumOpportunityResearchPoints} type.def {type.def.defName} type.rel {type.rel} (points: {typeResearchPoints} base imp.: {baseImportance} count:{matchCount}) points per: {(typeResearchPoints * baseImportance)}");

                    foreach (var opportunity in allMatchingOpportunitiesOfType)
                    {
                        var opportunityResearchPoints = LegacyOpportunitySemantics.CalculateOpportunityMaximum(
                            typeResearchPoints,
                            minimumOpportunityResearchPoints,
                            baseImportance,
                            opportunity.importance,
                            opportunity.IsRare);

                        opportunity.SetMaxProgress(opportunityResearchPoints);
                    }
                }
            }

            return (projectOpportunities, totalStores);
        }
    }
}
