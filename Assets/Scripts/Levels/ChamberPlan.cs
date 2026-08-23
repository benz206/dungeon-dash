using System.Collections.Generic;
using UnityEngine;

namespace DungeonDash
{
    public readonly struct PlacedProp
    {
        public PropDefinition Definition { get; }
        public Vector2 Position { get; }

        public PlacedProp(PropDefinition definition, Vector2 position)
        {
            Definition = definition;
            Position = position;
        }
    }

    public sealed class ChamberPlan
    {
        public DungeonLayout Layout { get; set; }
        public ChamberTheme Theme { get; set; }
        public Dictionary<Vector2Int, WallDecoration> WallDecorations { get; set; }
        public List<PlacedProp> Props { get; } = new();
        public List<Vector2> SpawnAnchors { get; } = new();
        public HashSet<Vector2Int> GrassCells { get; } = new();
        public HashSet<Vector2Int> ForcedColumns { get; } = new();
        public HashSet<Vector2Int> BlockedCells { get; } = new();
    }
}
