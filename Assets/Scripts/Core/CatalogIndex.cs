using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DungeonDash
{
    public sealed class CatalogIndex
    {
        readonly Dictionary<string, Sprite> _weaponSprites = new();
        readonly Dictionary<string, Sprite> _tiles = new();
        readonly Dictionary<string, GameCatalog.CharacterSkin> _characters = new();
        readonly Dictionary<string, GameCatalog.EnemySkin> _enemies = new();

        public GameCatalog Catalog { get; }
        public string[] ArtifactWeaponIds { get; }
        public Sprite[] CleanFloors { get; }
        public Sprite[] DamagedFloors { get; }

        public CatalogIndex(GameCatalog catalog)
        {
            Catalog = catalog;
            foreach (var weapon in catalog.weapons)
                if (weapon.sprite != null) _weaponSprites[weapon.id] = weapon.sprite;
            foreach (var tile in catalog.walls) _tiles[tile.name] = tile;
            foreach (var tile in catalog.floors) _tiles[tile.name] = tile;
            foreach (var tile in catalog.grass) _tiles[tile.name] = tile;
            foreach (var tile in catalog.paths) _tiles[tile.name] = tile;
            foreach (var skin in catalog.characters) _characters[skin.id] = skin;
            foreach (var skin in catalog.enemies) _enemies[skin.id] = skin;

            ArtifactWeaponIds = catalog.weapons
                .Where(x => WeaponRules.IsArtifactWeapon(x.id)).Select(x => x.id).ToArray();
            CleanFloors = DungeonTileSelector.CleanFloorPool(catalog.floors);
            DamagedFloors = DungeonTileSelector.DamagedFloorPool(catalog.floors);
        }

        public Sprite Weapon(string id) =>
            id != null && _weaponSprites.TryGetValue(id, out var sprite) ? sprite : null;

        public Sprite Tile(string name) => _tiles.TryGetValue(name, out var sprite) ? sprite : null;

        public GameCatalog.CharacterSkin Character(string id) =>
            id != null && _characters.TryGetValue(id, out var skin) ? skin : Catalog.characters[0];

        public GameCatalog.EnemySkin Enemy(string id) =>
            id != null && _enemies.TryGetValue(id, out var skin) ? skin : null;

        public Sprite Floor(float roomAge, System.Random random)
        {
            var pool = random.NextDouble() < DungeonTileSelector.DamagedFloorChance(roomAge)
                ? DamagedFloors
                : CleanFloors;
            return pool[random.Next(pool.Length)];
        }
    }
}
