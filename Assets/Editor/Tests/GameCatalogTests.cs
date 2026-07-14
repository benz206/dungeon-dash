using System.Collections.Generic;
using System.Linq;
using DungeonDash;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DungeonDashTests
{
    public sealed class GameCatalogTests
    {
        GameCatalog _catalog;

        [SetUp]
        public void SetUp()
        {
            _catalog = AssetDatabase.LoadAssetAtPath<GameCatalog>("Assets/Resources/GameCatalog.asset");
            Assert.That(_catalog, Is.Not.Null, "Generate the catalog from Tools/Dungeon Dash first.");
        }

        [Test]
        public void Catalog_MakesEveryCharacterVariantAndEnemyFamilyPlayable()
        {
            Assert.That(_catalog.characters, Has.Length.EqualTo(11));
            Assert.That(_catalog.enemies, Has.Length.EqualTo(21));
            Assert.That(_catalog.characters.All(x => x.idle.Length > 0 && x.run.Length > 0), Is.True);
            Assert.That(_catalog.enemies.All(x => x.idle.Length > 0 && x.run.Length > 0), Is.True);
        }

        [Test]
        public void Catalog_ReferencesEveryImportedGameplaySprite()
        {
            AssertComplete("Assets/Art/Characters", _catalog.characters
                .SelectMany(x => x.idle.Concat(x.run).Concat(x.hit)));
            AssertComplete("Assets/Art/Enemies", _catalog.enemies
                .SelectMany(x => x.idle.Concat(x.run)));
            AssertComplete("Assets/Art/Weapons", _catalog.weapons.Select(x => x.sprite));
            AssertComplete("Assets/Art/Tiles", _catalog.floors.Concat(_catalog.walls));
            AssertComplete("Assets/Art/Items", _catalog.coins.Concat(_catalog.potions)
                .Concat(_catalog.chests).Concat(_catalog.bombs));
            AssertComplete("Assets/Art/UI", new[]
            {
                _catalog.heartFull, _catalog.heartHalf, _catalog.heartEmpty,
                _catalog.buttonUp, _catalog.buttonDown,
                _catalog.dangerButtonUp, _catalog.dangerButtonDown
            });
        }

        static void AssertComplete(string folder, IEnumerable<Sprite> referenced)
        {
            var expected = AssetDatabase.FindAssets("t:Sprite", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath).ToHashSet();
            var actual = referenced.Where(x => x != null)
                .Select(AssetDatabase.GetAssetPath).ToHashSet();
            var missing = expected.Except(actual).ToArray();
            Assert.That(missing, Is.Empty, $"Unreferenced sprites under {folder}: {string.Join(", ", missing)}");
        }
    }
}
