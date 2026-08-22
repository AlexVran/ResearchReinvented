using PeteTimesSix.ResearchReinvented.Domain;
using PeteTimesSix.ResearchReinvented.Domain.Prototypes;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace PeteTimesSix.ResearchReinvented.Managers
{
    public sealed class PrototypeSaveRootData : IExposable
    {
        private int schemaVersion = PrototypeSaveSnapshot.CurrentSchemaVersion;
        private List<PrototypeSavedRecordData> records = new List<PrototypeSavedRecordData>();

        public PrototypeSaveRootData() { }

        public PrototypeSaveRootData(PrototypeSaveSnapshot snapshot)
        {
            schemaVersion = snapshot.SchemaVersion;
            records = snapshot.Records.Select(item => new PrototypeSavedRecordData(item)).ToList();
        }

        public PrototypeSaveSnapshot ToDomain() => new PrototypeSaveSnapshot(schemaVersion, (records ?? new List<PrototypeSavedRecordData>()).Select(item => item.ToDomain()));

        public void ExposeData()
        {
            Scribe_Values.Look(ref schemaVersion, "schemaVersion", PrototypeSaveSnapshot.CurrentSchemaVersion);
            Scribe_Collections.Look(ref records, "records", LookMode.Deep);
            records ??= new List<PrototypeSavedRecordData>();
        }
    }

    public sealed class PrototypeSavedRecordData : IExposable
    {
        private string key;
        private string projectDefName;
        private int kind;
        private int state;
        private string opportunityKey;
        private string subject;

        public PrototypeSavedRecordData() { }

        public PrototypeSavedRecordData(PrototypeSavedRecord record)
        {
            key = record.Key;
            projectDefName = record.Project?.DefName;
            kind = (int)record.Kind;
            state = (int)record.State;
            opportunityKey = record.OpportunityKey;
            subject = record.Subject;
        }

        public PrototypeSavedRecord ToDomain()
        {
            var project = string.IsNullOrWhiteSpace(projectDefName) ? null : new DefIdentity("ResearchProjectDef", projectDefName);
            return new PrototypeSavedRecord(key, project, (PrototypeArtifactKind)kind, (PrototypeLifecycleState)state, opportunityKey, subject);
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref key, "key");
            Scribe_Values.Look(ref projectDefName, "project");
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Values.Look(ref state, "state");
            Scribe_Values.Look(ref opportunityKey, "opportunityKey");
            Scribe_Values.Look(ref subject, "subject");
        }
    }
}
