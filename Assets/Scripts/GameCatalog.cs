using System;
using UnityEngine;

namespace DungeonDash
{
    [CreateAssetMenu(menuName = "Dungeon Dash/Game Catalog")]
    public sealed class GameCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class CharacterSkin
        {
            public string id;
            public Sprite[] idle;
            public Sprite[] run;
            public Sprite[] hit;
            public float speed;
            public float maxHealth;
            public float damageMod;
        }

        [Serializable]
        public sealed class EnemySkin
        {
            public string id;
            public Sprite[] idle;
            public Sprite[] run;
        }

        [Serializable]
        public sealed class NamedSprite
        {
            public string id;
            public Sprite sprite;
        }

        public CharacterSkin[] characters;
        public EnemySkin[] enemies;
        public NamedSprite[] weapons;
        public Sprite[] floors;
        public Sprite[] walls;
        public Sprite[] coins;
        public Sprite[] potions;
        public Sprite[] chests;
        public Sprite[] bombs;
        public Sprite heartFull;
        public Sprite heartHalf;
        public Sprite heartEmpty;
        public Sprite buttonUp;
        public Sprite buttonDown;
        public Sprite dangerButtonUp;
        public Sprite dangerButtonDown;
    }
}
