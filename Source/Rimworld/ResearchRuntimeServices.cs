using System;
using System.Collections.Generic;
using PeteTimesSix.ResearchReinvented.Utilities;
using RimWorld;
using Verse;

namespace PeteTimesSix.ResearchReinvented.Rimworld
{
	internal interface IResearchRuntimeServices
	{
		IEnumerable<TDef> AllDefs<TDef>() where TDef : Def;

		IReadOnlyList<TDef> AllDefsListForReading<TDef>() where TDef : Def;

		ResearchManager ResearchManager { get; }

		ResearchProjectDef CurrentResearchProject { get; }

		Faction PlayerFaction { get; }

		IEnumerable<Faction> ResearchFactions { get; }

		IReadOnlyList<Map> Maps { get; }

		ResearchReinvented_Settings Settings { get; }

		TComponent GetGameComponent<TComponent>() where TComponent : GameComponent;
	}

	internal sealed class LiveResearchRuntimeServices : IResearchRuntimeServices
	{
		public IEnumerable<TDef> AllDefs<TDef>() where TDef : Def
		{
			return DefDatabase<TDef>.AllDefs;
		}

		public IReadOnlyList<TDef> AllDefsListForReading<TDef>() where TDef : Def
		{
			return DefDatabase<TDef>.AllDefsListForReading;
		}

		public ResearchManager ResearchManager => Find.ResearchManager;

		public ResearchProjectDef CurrentResearchProject => ResearchManager.GetProject();

		public Faction PlayerFaction => Faction.OfPlayer;

		public IEnumerable<Faction> ResearchFactions => Find.FactionManager.GetFactions(
			allowHidden: true,
			allowDefeated: true,
			allowNonHumanlike: false,
			minTechLevel: TechLevel.Neolithic,
			allowTemporary: false);

		public IReadOnlyList<Map> Maps => Find.Maps;

		public ResearchReinvented_Settings Settings => ResearchReinventedMod.Settings;

		public TComponent GetGameComponent<TComponent>() where TComponent : GameComponent
		{
			return Current.Game.GetComponent<TComponent>();
		}
	}

	internal static class ResearchRuntimeServices
	{
		private static readonly ScopedServiceOverride<IResearchRuntimeServices> services =
			new ScopedServiceOverride<IResearchRuntimeServices>(new LiveResearchRuntimeServices());

		internal static IResearchRuntimeServices Current => services.Current;

		internal static IDisposable OverrideForTests(IResearchRuntimeServices replacement)
		{
			return services.Push(replacement);
		}
	}
}
