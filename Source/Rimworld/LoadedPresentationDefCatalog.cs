using PeteTimesSix.ResearchReinvented.Defs;
using PeteTimesSix.ResearchReinvented.Domain;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace PeteTimesSix.ResearchReinvented.Rimworld
{
    /// <summary>
    /// Immutable loaded-Def lookup built once after Def loading. Presentation
    /// snapshots resolve identities through it without repeating global scans.
    /// </summary>
    internal static class LoadedPresentationDefCatalog
    {
        private static IReadOnlyDictionary<string, ResearchOpportunityCategoryDef> categories =
            new ReadOnlyDictionary<string, ResearchOpportunityCategoryDef>(new Dictionary<string, ResearchOpportunityCategoryDef>());
        private static IReadOnlyDictionary<string, ResearchOpportunityTypeDef> types =
            new ReadOnlyDictionary<string, ResearchOpportunityTypeDef>(new Dictionary<string, ResearchOpportunityTypeDef>());

        internal static void Initialize(IResearchRuntimeServices services)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            categories = new ReadOnlyDictionary<string, ResearchOpportunityCategoryDef>(services.AllDefsListForReading<ResearchOpportunityCategoryDef>()
                .Where(item => item != null && !string.IsNullOrEmpty(item.defName))
                .GroupBy(item => item.defName, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal));
            types = new ReadOnlyDictionary<string, ResearchOpportunityTypeDef>(services.AllDefsListForReading<ResearchOpportunityTypeDef>()
                .Where(item => item != null && !string.IsNullOrEmpty(item.defName))
                .GroupBy(item => item.defName, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal));
        }

        internal static ResearchOpportunityCategoryDef Category(DefIdentity identity) =>
            identity != null && categories.TryGetValue(identity.DefName, out var value) ? value : null;

        internal static ResearchOpportunityTypeDef Type(DefIdentity identity) =>
            identity != null && types.TryGetValue(identity.DefName, out var value) ? value : null;
    }
}
