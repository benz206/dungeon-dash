using System.Collections.Generic;
using UnityEngine;

namespace DungeonDash
{
    public sealed class BuiltWorld
    {
        public Transform Root { get; set; }
        public HashSet<Vector2Int> Walkable { get; } = new();
        public List<Vector2> SpawnAnchors { get; } = new();
        public Vector2 EntryPoint { get; set; }
        public ChamberTheme Theme { get; set; }
        public ExitDoor ExitDoor { get; set; }
    }
}
