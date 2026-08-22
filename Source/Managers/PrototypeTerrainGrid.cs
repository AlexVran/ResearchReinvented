using RimWorld;
using PeteTimesSix.ResearchReinvented.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace PeteTimesSix.ResearchReinvented.Managers
{
    public class PrototypeTerrainGrid : MapComponent
    {
        private ByteGrid terrainGrid;
        private ByteGrid foundationGrid;
        private HashSet<Thing> prototypes = new HashSet<Thing>();

        public PrototypeTerrainGrid(Map map): base(map)
        {
            this.terrainGrid = new ByteGrid(map);
            this.foundationGrid = new ByteGrid(map);
        }

        public bool IsTerrainPrototype(IntVec3 position)
        {
            return terrainGrid[map.cellIndices.CellToIndex(position)] != 0;
        }

        public IReadOnlyCollection<Thing> Prototypes => prototypes;

        public bool IsPrototype(Thing thing)
        {
            if (thing == null)
                return false;
            var unwrapped = thing.UnwrapIfWrapped();
            return prototypes.Contains(thing) || (unwrapped != thing && prototypes.Contains(unwrapped));
        }

        public void MarkAsPrototype(Thing thing)
        {
            if (thing != null)
                prototypes.Add(thing.UnwrapIfWrapped());
        }

        public void UnmarkAsPrototype(Thing thing)
        {
            if (thing == null)
                return;
            prototypes.Remove(thing);
            prototypes.Remove(thing.UnwrapIfWrapped());
        }

        public void MarkTerrainAsPrototype(IntVec3 position, TerrainDef terrain)
        {
            terrainGrid[map.cellIndices.CellToIndex(position)] = 1;
        }

        public void UnmarkTerrainAsPrototype(IntVec3 position)
        {
            terrainGrid[map.cellIndices.CellToIndex(position)] = 0;
        }

        public bool IsFoundationTerrainPrototype(IntVec3 position)
        {
            return foundationGrid[map.cellIndices.CellToIndex(position)] != 0;
        }

        public void MarkFoundationTerrainAsPrototype(IntVec3 position, TerrainDef terrain)
        {
            foundationGrid[map.cellIndices.CellToIndex(position)] = 1;
        }

        public void UnmarkFoundationTerrainAsPrototype(IntVec3 position)
        {
            foundationGrid[map.cellIndices.CellToIndex(position)] = 0;
        }


        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.Saving)
                prototypes.RemoveWhere(thing => thing == null || thing.Destroyed);
            Scribe_Collections.Look(ref prototypes, "prototypes", LookMode.Reference);
            Scribe_Deep.Look(ref terrainGrid, "terrainGrid");
            Scribe_Deep.Look(ref foundationGrid, "foundationGrid");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                prototypes ??= new HashSet<Thing>();
        }

        public void DebugDrawOnMap()
        {
            var mapSizeX = map.Size.x;
            for (int i = 0; i < terrainGrid.CellsCount; i++)
            {
                var cell = CellIndicesUtility.IndexToCell(i, mapSizeX);
                bool isTerrainProto = IsTerrainPrototype(cell);
                bool isFoundationProto = IsFoundationTerrainPrototype(cell);
                Color color = Color.black;
                if (isTerrainProto && isFoundationProto)
                    color = Color.red;
                else if (isTerrainProto)
                    color = Color.green;
                else if (isFoundationProto)
                    color = Color.blue;

                if (color != Color.black)
                    CellRenderer.RenderCell(cell, SolidColorMaterials.SimpleSolidColorMaterial(color, false));
            }
        }
    }
}
