using System;
using System.Collections.Generic;
using UnityEngine;

namespace DungeonDash
{
    [Serializable]
    public sealed class RunCheckpoint
    {
        // Increment when chamber generation or the saved combat format changes.
        public const int CurrentVersion = 1;
        public int version = CurrentVersion;
        public int seed;
        public bool usesLevelLibrary;
        public int wave;
        public int kills;
        public int waveSize;
        public int enemyCursor;
        public int weaponCursor;
        public Vector2 playerPosition;
        public int health;
        public List<Enemy> enemies = new();
        public List<Pickup> pickups = new();

        public bool CanResume => version == CurrentVersion && wave > 0 && health > 0 &&
            enemies != null && pickups != null;

        [Serializable]
        public sealed class Enemy
        {
            public string skinId;
            public Vector2 position;
            public int health;
        }

        [Serializable]
        public sealed class Pickup
        {
            public PickupKind kind;
            public Vector2 position;
            public Artifact artifact;
            public float remainingLifetime;
        }
    }
}
