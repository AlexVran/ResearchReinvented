using PeteTimesSix.ResearchReinvented.Managers;
using PeteTimesSix.ResearchReinvented.Opportunities;
using PeteTimesSix.ResearchReinvented.OpportunityComps;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using Verse;

namespace ResearchReinventedRewrite.Phase0
{
    public sealed class LegacyGeneratorCollector : GameComponent
    {
        private static readonly FieldInfo MaximumProgressField = typeof(ResearchOpportunity)
            .GetField("maximumProgress", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo CurrentProgressField = typeof(ResearchOpportunity)
            .GetField("currentProgress", BindingFlags.Instance | BindingFlags.NonPublic);

        private static bool collected;

        public LegacyGeneratorCollector(Game game)
        {
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();

            string outputPath = Environment.GetEnvironmentVariable("RR_BASELINE_OUTPUT");
            if (collected || outputPath.NullOrEmpty())
                return;

            collected = true;
            LongEventHandler.ExecuteWhenFinished(() => Collect(outputPath));
        }

        private static void Collect(string outputPath)
        {
            var report = new CollectorReport
            {
                schema = "research-reinvented-legacy-generator-baseline-v1",
                capturedAtUtc = DateTime.UtcNow.ToString("O"),
                gameVersion = VersionControl.CurrentVersionStringWithRev,
                loadedDefs = new List<DefCountSnapshot>
                {
                    new DefCountSnapshot(nameof(ResearchProjectDef), DefDatabase<ResearchProjectDef>.DefCount),
                    new DefCountSnapshot(nameof(ThingDef), DefDatabase<ThingDef>.DefCount),
                    new DefCountSnapshot(nameof(RecipeDef), DefDatabase<RecipeDef>.DefCount),
                    new DefCountSnapshot(nameof(TerrainDef), DefDatabase<TerrainDef>.DefCount),
                    new DefCountSnapshot(nameof(FactionDef), DefDatabase<FactionDef>.DefCount),
                },
                mods = LoadedModManager.RunningModsListForReading
                    .Select(mod => new ModSnapshot
                    {
                        packageId = mod.PackageIdPlayerFacing,
                        name = mod.Name,
                    })
                    .ToList(),
            };

            try
            {
                foreach (string defName in ProjectNames())
                {
                    ResearchProjectDef project = DefDatabase<ResearchProjectDef>.GetNamedSilentFail(defName);
                    if (project == null)
                    {
                        report.missingProjects.Add(defName);
                        continue;
                    }

                    report.projects.Add(CollectProject(project));
                }

                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                File.WriteAllText(outputPath, CollectorJson.Serialize(report));
                Log.Message($"RR Phase 0 collector wrote {report.projects.Count} project snapshots to {outputPath}");
            }
            catch (Exception exception)
            {
                report.error = exception.ToString();
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                File.WriteAllText(outputPath, CollectorJson.Serialize(report));
                Log.Error($"RR Phase 0 collector failed: {exception}");
            }
            finally
            {
                if (Environment.GetEnvironmentVariable("RR_BASELINE_EXIT") == "1")
                    Application.Quit();
            }
        }

        private static ProjectSnapshot CollectProject(ResearchProjectDef project)
        {
            long memoryBefore = GC.GetTotalMemory(false);
            var stopwatch = Stopwatch.StartNew();
            var generated = ResearchOpportunityPrefabs.MakeOpportunitiesForProject(project);
            stopwatch.Stop();
            long memoryAfter = GC.GetTotalMemory(false);

            return new ProjectSnapshot
            {
                defName = project.defName,
                modPackageId = project.modContentPack?.PackageIdPlayerFacing,
                baseCost = project.baseCost,
                elapsedMilliseconds = stopwatch.Elapsed.TotalMilliseconds,
                approximateManagedBytesDelta = memoryAfter - memoryBefore,
                opportunities = generated.opportunities
                    .Select(SnapshotOpportunity)
                    .OrderBy(opportunity => opportunity.type, StringComparer.Ordinal)
                    .ThenBy(opportunity => opportunity.relation, StringComparer.Ordinal)
                    .ThenBy(opportunity => opportunity.requirement.canonicalIdentity, StringComparer.Ordinal)
                    .ToList(),
                categoryStores = generated.categoryStores
                    .Select(store => new CategoryStoreSnapshot
                    {
                        category = store.category.defName,
                        researchPoints = store.researchPoints,
                    })
                    .OrderBy(store => store.category, StringComparer.Ordinal)
                    .ToList(),
            };
        }

        private static OpportunitySnapshot SnapshotOpportunity(ResearchOpportunity opportunity)
        {
            return new OpportunitySnapshot
            {
                project = opportunity.project.defName,
                type = opportunity.def.defName,
                relation = opportunity.relation.ToString(),
                category = opportunity.def.GetCategory(opportunity.relation)?.defName,
                maximumProgress = (float)MaximumProgressField.GetValue(opportunity),
                currentProgress = (float)CurrentProgressField.GetValue(opportunity),
                importance = opportunity.importance,
                rare = opportunity.IsRare,
                freebie = opportunity.IsFreebie,
                generationSource = opportunity.debug_source,
                requirement = SnapshotRequirement(opportunity.requirement),
            };
        }

        private static RequirementSnapshot SnapshotRequirement(ResearchOpportunityComp requirement)
        {
            var snapshot = new RequirementSnapshot
            {
                kind = requirement?.GetType().Name,
                displaySubject = requirement?.Subject.ToString(),
            };

            switch (requirement)
            {
                case ROComp_RequiresThing thing:
                    snapshot.canonicalIdentity = thing.PrimaryThingDef?.defName;
                    snapshot.alternates = DefNames(thing.Alternates);
                    break;
                case ROComp_RequiresRecipe recipe:
                    snapshot.canonicalIdentity = recipe.AllRecipes.FirstOrDefault()?.defName;
                    snapshot.alternates = DefNames(recipe.Alternates);
                    break;
                case ROComp_RequiresTerrain terrain:
                    snapshot.canonicalIdentity = terrain.AllTerrains.FirstOrDefault()?.defName;
                    snapshot.alternates = DefNames(terrain.Alternates);
                    break;
                case ROComp_RequiresFaction faction:
                    snapshot.canonicalIdentity = $"{faction.faction?.def?.defName}:{faction.faction?.GetUniqueLoadID()}";
                    break;
                case ROComp_RequiresSchematicWithProject schematic:
                    snapshot.canonicalIdentity = schematic.projectDef?.defName;
                    break;
                case ROComp_RequiresFactionlessPawn _:
                    snapshot.canonicalIdentity = "factionless-pawn";
                    break;
                case ROComp_RequiresNothing _:
                    snapshot.canonicalIdentity = "none";
                    break;
                default:
                    snapshot.canonicalIdentity = requirement?.ShortDesc;
                    break;
            }

            return snapshot;
        }

        private static List<string> DefNames(IEnumerable<Def> defs)
        {
            if (defs == null)
                return new List<string>();
            return defs.Where(def => def != null).Select(def => def.defName).OrderBy(name => name).ToList();
        }

        private static IEnumerable<string> ProjectNames()
        {
            string configured = Environment.GetEnvironmentVariable("RR_BASELINE_PROJECTS");
            if (!configured.NullOrEmpty())
            {
                return configured.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(value => value.Trim())
                    .Where(value => value.Length > 0);
            }

            return new[]
            {
                "Electricity",
                "ComplexClothing",
                "DrugProduction",
                "MedicineProduction",
                "Prosthetics",
                "Bionics",
                "TreeSowing",
                "CarpetMaking",
                "BiofuelRefining",
                "MicroelectronicsBasics",
                "MultiAnalyzer",
                "BioferriteShaping",
                "BlissLobotomy",
                "Ferny_ButcherTable",
                "Ferny_Workbench",
            };
        }
    }

    [Serializable]
    public sealed class CollectorReport
    {
        public string schema;
        public string capturedAtUtc;
        public string gameVersion;
        public List<DefCountSnapshot> loadedDefs;
        public List<ModSnapshot> mods = new List<ModSnapshot>();
        public List<ProjectSnapshot> projects = new List<ProjectSnapshot>();
        public List<string> missingProjects = new List<string>();
        public string error;
    }

    [Serializable]
    public sealed class DefCountSnapshot
    {
        public string defType;
        public int count;

        public DefCountSnapshot(string defType, int count)
        {
            this.defType = defType;
            this.count = count;
        }
    }

    [Serializable]
    public sealed class ModSnapshot
    {
        public string packageId;
        public string name;
    }

    [Serializable]
    public sealed class ProjectSnapshot
    {
        public string defName;
        public string modPackageId;
        public float baseCost;
        public double elapsedMilliseconds;
        public long approximateManagedBytesDelta;
        public List<OpportunitySnapshot> opportunities;
        public List<CategoryStoreSnapshot> categoryStores;
    }

    [Serializable]
    public sealed class OpportunitySnapshot
    {
        public string project;
        public string type;
        public string relation;
        public string category;
        public float maximumProgress;
        public float currentProgress;
        public float importance;
        public bool rare;
        public bool freebie;
        public string generationSource;
        public RequirementSnapshot requirement;
    }

    [Serializable]
    public sealed class RequirementSnapshot
    {
        public string kind;
        public string canonicalIdentity;
        public string displaySubject;
        public List<string> alternates = new List<string>();
    }

    [Serializable]
    public sealed class CategoryStoreSnapshot
    {
        public string category;
        public float researchPoints;
    }

    internal static class CollectorJson
    {
        public static string Serialize(CollectorReport report)
        {
            var json = new StringBuilder();
            json.Append('{');
            AppendStringProperty(json, "schema", report.schema);
            json.Append(',');
            AppendStringProperty(json, "capturedAtUtc", report.capturedAtUtc);
            json.Append(',');
            AppendStringProperty(json, "gameVersion", report.gameVersion);
            json.Append(',');
            AppendPropertyName(json, "loadedDefs");
            AppendArray(json, report.loadedDefs, AppendDefCount);
            json.Append(',');
            AppendPropertyName(json, "mods");
            AppendArray(json, report.mods, AppendMod);
            json.Append(',');
            AppendPropertyName(json, "projects");
            AppendArray(json, report.projects, AppendProject);
            json.Append(',');
            AppendPropertyName(json, "missingProjects");
            AppendArray(json, report.missingProjects, AppendString);
            json.Append(',');
            AppendStringProperty(json, "error", report.error);
            json.Append('}');
            return json.ToString();
        }

        private static void AppendDefCount(StringBuilder json, DefCountSnapshot snapshot)
        {
            json.Append('{');
            AppendStringProperty(json, "defType", snapshot.defType);
            json.Append(',');
            AppendNumberProperty(json, "count", snapshot.count);
            json.Append('}');
        }

        private static void AppendMod(StringBuilder json, ModSnapshot snapshot)
        {
            json.Append('{');
            AppendStringProperty(json, "packageId", snapshot.packageId);
            json.Append(',');
            AppendStringProperty(json, "name", snapshot.name);
            json.Append('}');
        }

        private static void AppendProject(StringBuilder json, ProjectSnapshot snapshot)
        {
            json.Append('{');
            AppendStringProperty(json, "defName", snapshot.defName);
            json.Append(',');
            AppendStringProperty(json, "modPackageId", snapshot.modPackageId);
            json.Append(',');
            AppendNumberProperty(json, "baseCost", snapshot.baseCost);
            json.Append(',');
            AppendNumberProperty(json, "elapsedMilliseconds", snapshot.elapsedMilliseconds);
            json.Append(',');
            AppendNumberProperty(json, "approximateManagedBytesDelta", snapshot.approximateManagedBytesDelta);
            json.Append(',');
            AppendPropertyName(json, "opportunities");
            AppendArray(json, snapshot.opportunities, AppendOpportunity);
            json.Append(',');
            AppendPropertyName(json, "categoryStores");
            AppendArray(json, snapshot.categoryStores, AppendCategoryStore);
            json.Append('}');
        }

        private static void AppendOpportunity(StringBuilder json, OpportunitySnapshot snapshot)
        {
            json.Append('{');
            AppendStringProperty(json, "project", snapshot.project);
            json.Append(',');
            AppendStringProperty(json, "type", snapshot.type);
            json.Append(',');
            AppendStringProperty(json, "relation", snapshot.relation);
            json.Append(',');
            AppendStringProperty(json, "category", snapshot.category);
            json.Append(',');
            AppendNumberProperty(json, "maximumProgress", snapshot.maximumProgress);
            json.Append(',');
            AppendNumberProperty(json, "currentProgress", snapshot.currentProgress);
            json.Append(',');
            AppendNumberProperty(json, "importance", snapshot.importance);
            json.Append(',');
            AppendBooleanProperty(json, "rare", snapshot.rare);
            json.Append(',');
            AppendBooleanProperty(json, "freebie", snapshot.freebie);
            json.Append(',');
            AppendStringProperty(json, "generationSource", snapshot.generationSource);
            json.Append(',');
            AppendPropertyName(json, "requirement");
            AppendRequirement(json, snapshot.requirement);
            json.Append('}');
        }

        private static void AppendRequirement(StringBuilder json, RequirementSnapshot snapshot)
        {
            if (snapshot == null)
            {
                json.Append("null");
                return;
            }

            json.Append('{');
            AppendStringProperty(json, "kind", snapshot.kind);
            json.Append(',');
            AppendStringProperty(json, "canonicalIdentity", snapshot.canonicalIdentity);
            json.Append(',');
            AppendStringProperty(json, "displaySubject", snapshot.displaySubject);
            json.Append(',');
            AppendPropertyName(json, "alternates");
            AppendArray(json, snapshot.alternates, AppendString);
            json.Append('}');
        }

        private static void AppendCategoryStore(StringBuilder json, CategoryStoreSnapshot snapshot)
        {
            json.Append('{');
            AppendStringProperty(json, "category", snapshot.category);
            json.Append(',');
            AppendNumberProperty(json, "researchPoints", snapshot.researchPoints);
            json.Append('}');
        }

        private static void AppendArray<T>(StringBuilder json, IEnumerable<T> values, Action<StringBuilder, T> appendItem)
        {
            if (values == null)
            {
                json.Append("null");
                return;
            }

            json.Append('[');
            bool first = true;
            foreach (T value in values)
            {
                if (!first)
                    json.Append(',');
                first = false;
                appendItem(json, value);
            }
            json.Append(']');
        }

        private static void AppendStringProperty(StringBuilder json, string name, string value)
        {
            AppendPropertyName(json, name);
            AppendString(json, value);
        }

        private static void AppendNumberProperty(StringBuilder json, string name, int value)
        {
            AppendPropertyName(json, name);
            json.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static void AppendNumberProperty(StringBuilder json, string name, long value)
        {
            AppendPropertyName(json, name);
            json.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static void AppendNumberProperty(StringBuilder json, string name, float value)
        {
            AppendPropertyName(json, name);
            json.Append(value.ToString("R", CultureInfo.InvariantCulture));
        }

        private static void AppendNumberProperty(StringBuilder json, string name, double value)
        {
            AppendPropertyName(json, name);
            json.Append(value.ToString("R", CultureInfo.InvariantCulture));
        }

        private static void AppendBooleanProperty(StringBuilder json, string name, bool value)
        {
            AppendPropertyName(json, name);
            json.Append(value ? "true" : "false");
        }

        private static void AppendPropertyName(StringBuilder json, string name)
        {
            AppendString(json, name);
            json.Append(':');
        }

        private static void AppendString(StringBuilder json, string value)
        {
            if (value == null)
            {
                json.Append("null");
                return;
            }

            json.Append('"');
            foreach (char character in value)
            {
                switch (character)
                {
                    case '"':
                        json.Append("\\\"");
                        break;
                    case '\\':
                        json.Append("\\\\");
                        break;
                    case '\b':
                        json.Append("\\b");
                        break;
                    case '\f':
                        json.Append("\\f");
                        break;
                    case '\n':
                        json.Append("\\n");
                        break;
                    case '\r':
                        json.Append("\\r");
                        break;
                    case '\t':
                        json.Append("\\t");
                        break;
                    default:
                        if (character < 0x20)
                            json.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        else
                            json.Append(character);
                        break;
                }
            }
            json.Append('"');
        }
    }
}
